using System.Net;
using System.Text.Json;
using Fido2NetLib;
using GAToolAPI.Models;
using GAToolAPI.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace GAToolAPI.Controllers;

[ApiController]
[Route("v3/auth")]
[OpenApiTag("Authentication")]
[EnableCors("AuthOrigins")]
public class AuthController(
    OtpService otp,
    TokenService tokens,
    PasskeyService passkeys,
    AuthRepository repo,
    ILogger<AuthController> logger) : ControllerBase
{
    private string? UserAgent => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    private static bool LooksLikeEmail(string s) =>
        !string.IsNullOrWhiteSpace(s) && s.Contains('@') && s.Length <= 254;

    // ── OTP login ────────────────────────────────────────────────────────────

    /// <summary>Request a one-time login code be emailed to the given address.</summary>
    /// <response code="204">Code sent (or rate-limited; response is the same to avoid email enumeration).</response>
    [HttpPost("otp/request")]
    [AllowAnonymous]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> RequestOtp([FromBody] OtpRequestBody body, CancellationToken ct)
    {
        if (!LooksLikeEmail(body.Email))
            return BadRequest(new { message = "Invalid email" });

        var result = await otp.IssueAsync(body.Email, ct);
        // Always return 204 — never reveal whether the email is rate-limited or send failed.
        // Operators can check logs / SES bounce metrics for delivery issues.
        if (result == OtpService.IssueResult.RateLimited)
            logger.LogInformation("OTP rate-limited for {Email}", body.Email);
        return NoContent();
    }

    /// <summary>Exchange an OTP code for an access token + refresh token.</summary>
    [HttpPost("otp/verify")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyBody body, CancellationToken ct)
    {
        if (!LooksLikeEmail(body.Email) || string.IsNullOrWhiteSpace(body.Code))
            return BadRequest(new { message = "Email and code are required" });

        var result = await otp.VerifyAsync(body.Email, body.Code, ct);
        if (result != OtpService.VerifyResult.Ok)
            return Unauthorized(new { message = "Invalid or expired code", reason = result.ToString() });

        var user = await repo.UpsertUserAsync(body.Email, rolesIfNew: [], ct: ct);
        await repo.TouchLoginAsync(user.Email, ct);
        var resp = await tokens.IssueTokensAsync(user, UserAgent, ct);
        return Ok(resp);
    }

    // ── Refresh / logout ─────────────────────────────────────────────────────

    /// <summary>Exchange a refresh token for a new access token + refresh token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.RefreshToken))
            return BadRequest();
        var resp = await tokens.RefreshAsync(body.RefreshToken, UserAgent, ct);
        if (resp == null) return Unauthorized();
        return Ok(resp);
    }

    /// <summary>Revoke the supplied refresh token.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutBody body, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(body.RefreshToken))
            await tokens.RevokeRefreshTokenAsync(body.RefreshToken, ct);
        return NoContent();
    }

    // ── Current user info ────────────────────────────────────────────────────

    /// <summary>Get the currently-authenticated user's email, roles, and registered passkeys.</summary>
    [HttpGet("me")]
    [Authorize("user")]
    [ProducesResponseType(typeof(MeResponse), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var email = User.FindFirst("name")?.Value;
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var user = await repo.GetUserAsync(email, ct);
        if (user == null) return Unauthorized();

        var pks = await repo.ListPasskeysAsync(email, ct);
        return Ok(new MeResponse(
            user.Email,
            user.Roles,
            pks.Select(p => new PasskeyInfo(p.CredentialId, p.Nickname, p.CreatedAt, p.LastUsedAt))
                .ToArray()));
    }

    // ── Passkey registration ─────────────────────────────────────────────────

    /// <summary>Begin passkey registration. Returns WebAuthn creation options + a session id.</summary>
    [HttpPost("passkey/register-options")]
    [Authorize("user")]
    public async Task<IActionResult> PasskeyRegisterOptions(CancellationToken ct)
    {
        var email = User.FindFirst("name")?.Value;
        if (string.IsNullOrEmpty(email)) return Unauthorized();
        var result = await passkeys.BeginRegistrationAsync(email, ct);
        // Return the options as raw JSON (the JSON the browser expects) plus our sessionId.
        return Ok(new
        {
            sessionId = result.SessionId,
            options = JsonDocument.Parse(result.Options.ToJson()).RootElement
        });
    }

    public record PasskeyRegisterCompleteRequest(
        string SessionId,
        string? Nickname,
        AuthenticatorAttestationRawResponse Attestation);

    /// <summary>Complete passkey registration with the browser's attestation response.</summary>
    [HttpPost("passkey/register")]
    [Authorize("user")]
    public async Task<IActionResult> PasskeyRegister([FromBody] PasskeyRegisterCompleteRequest body,
        CancellationToken ct)
    {
        var email = User.FindFirst("name")?.Value;
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var record = await passkeys.CompleteRegistrationAsync(email, body.SessionId, body.Attestation,
            body.Nickname, ct);
        if (record == null) return BadRequest(new { message = "Registration session expired or invalid" });

        return Ok(new PasskeyInfo(record.CredentialId, record.Nickname, record.CreatedAt, record.LastUsedAt));
    }

    /// <summary>Remove a registered passkey from the current user.</summary>
    [HttpDelete("passkey/{credentialId}")]
    [Authorize("user")]
    public async Task<IActionResult> PasskeyDelete(string credentialId, CancellationToken ct)
    {
        var email = User.FindFirst("name")?.Value;
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        // Verify the passkey belongs to the caller before deleting
        var existing = await repo.GetPasskeyByCredentialIdAsync(credentialId, ct);
        if (existing == null || !string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase))
            return NotFound();

        await repo.DeletePasskeyAsync(email, credentialId, ct);
        return NoContent();
    }

    // ── Passkey authentication ───────────────────────────────────────────────

    /// <summary>Begin passkey authentication. Returns WebAuthn assertion options + a session id.</summary>
    [HttpPost("passkey/auth-options")]
    [AllowAnonymous]
    public async Task<IActionResult> PasskeyAuthOptions([FromBody] PasskeyAuthOptionsBody body,
        CancellationToken ct)
    {
        var result = await passkeys.BeginAuthenticationAsync(body.Email, ct);
        return Ok(new
        {
            sessionId = result.SessionId,
            options = JsonDocument.Parse(result.Options.ToJson()).RootElement
        });
    }

    public record PasskeyAuthCompleteRequest(
        string SessionId,
        AuthenticatorAssertionRawResponse Assertion);

    /// <summary>Complete passkey authentication and exchange for tokens.</summary>
    [HttpPost("passkey/authenticate")]
    [AllowAnonymous]
    public async Task<IActionResult> PasskeyAuthenticate([FromBody] PasskeyAuthCompleteRequest body,
        CancellationToken ct)
    {
        var user = await passkeys.CompleteAuthenticationAsync(body.SessionId, body.Assertion, ct);
        if (user == null) return Unauthorized(new { message = "Passkey authentication failed" });

        await repo.TouchLoginAsync(user.Email, ct);
        var resp = await tokens.IssueTokensAsync(user, UserAgent, ct);
        return Ok(resp);
    }
}
