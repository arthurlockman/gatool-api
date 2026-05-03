using System.Text.Json.Serialization;

namespace GAToolAPI.Models;

// ── Public DTOs (request/response shapes) ────────────────────────────────────

public record OtpRequestBody(string Email);

public record OtpVerifyBody(string Email, string Code);

public record RefreshBody(string RefreshToken);

public record LogoutBody(string RefreshToken);

public record TokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string Email,
    string[] Roles);

public record MeResponse(
    string Email,
    string[] Roles,
    PasskeyInfo[] Passkeys);

public record PasskeyInfo(
    string CredentialId,
    string? Nickname,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt);

// ── WebAuthn DTOs (we pass through Fido2 library options as JsonElement) ─────

public record PasskeyRegisterCompleteBody(
    string Nickname,
    System.Text.Json.JsonElement AttestationResponse);

public record PasskeyAuthOptionsBody(string? Email);

public record PasskeyAuthCompleteBody(
    System.Text.Json.JsonElement AssertionResponse);

// ── Internal storage records (DynamoDB-shaped, but kept as POCOs) ────────────

public class UserRecord
{
    public string Email { get; set; } = "";
    public string[] Roles { get; set; } = ["user"];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}

public class PasskeyRecord
{
    public string Email { get; set; } = "";
    public string CredentialId { get; set; } = ""; // base64url
    public byte[] PublicKey { get; set; } = [];
    public uint SignCount { get; set; }
    public Guid AaGuid { get; set; }
    public string? Nickname { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public string[] Transports { get; set; } = [];
}

public class OtpRecord
{
    public string Email { get; set; } = "";
    public string CodeHash { get; set; } = ""; // SHA-256 of the code
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int AttemptsRemaining { get; set; }
}

public class RefreshTokenRecord
{
    public string TokenHash { get; set; } = ""; // SHA-256 of the opaque token
    public string Email { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? UserAgent { get; set; }
}

// WebAuthn challenge state (cached briefly between options + complete calls)
public class WebAuthnChallengeState
{
    [JsonPropertyName("optionsJson")]
    public string OptionsJson { get; set; } = "";

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}
