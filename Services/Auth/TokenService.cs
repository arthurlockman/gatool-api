using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GAToolAPI.AuthExtensions;
using GAToolAPI.Models;
using Microsoft.IdentityModel.Tokens;

namespace GAToolAPI.Services.Auth;

/// <summary>
///     Issues and validates self-signed JWT access tokens, and manages opaque refresh tokens.
///
///     Access tokens:
///       - Algorithm: ES256 (ECDSA P-256 + SHA-256)
///       - Issuer: https://api.gatool.org/auth
///       - Audience: gatool
///       - Lifetime: 15 minutes
///       - Claims: sub, email, name, https://gatool.org/roles (one per role), iat, exp, jti
///
///     Refresh tokens:
///       - Opaque, 256-bit random URL-safe string
///       - Stored in DynamoDB as SHA-256 hash (never plaintext)
///       - Lifetime: 30 days, sliding (renewed on each refresh)
/// </summary>
public class TokenService(AuthSigningKeyProvider keyProvider, AuthRepository repo)
{
    public const string Issuer = "https://api.gatool.org/auth";
    private const string Audience = "gatool";
    private const string RolesClaim = AuthRoles.ClaimType;
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<TokenResponse> IssueTokensAsync(UserRecord user, string? userAgent = null,
        CancellationToken ct = default)
    {
        var accessToken = await CreateAccessTokenAsync(user, ct);
        var refresh = CreateRefreshToken();
        await repo.SaveRefreshTokenAsync(new RefreshTokenRecord
        {
            TokenHash = Sha256Hex(refresh),
            Email = user.Email,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(RefreshTokenLifetime),
            UserAgent = userAgent
        }, ct);

        return new TokenResponse(
            accessToken,
            refresh,
            (int)AccessTokenLifetime.TotalSeconds,
            user.Email,
            user.Roles);
    }

    /// <summary>
    /// Validate a refresh token, atomically consume it, and issue a new access + refresh pair.
    /// Returns null if the refresh token is unknown, expired, revoked, or was already
    /// consumed by another concurrent request (replay protection).
    /// </summary>
    public async Task<TokenResponse?> RefreshAsync(string refreshToken, string? userAgent = null,
        CancellationToken ct = default)
    {
        var hash = Sha256Hex(refreshToken);
        var record = await repo.GetRefreshTokenAsync(hash, ct);
        if (record == null) return null;

        // Atomically delete the old token. If two concurrent refreshes use the same token,
        // only the first wins; the second sees a ConditionalCheckFailed and returns null.
        // This is the core of refresh-token rotation as a replay-detection mechanism.
        if (!await repo.TryConsumeRefreshTokenAsync(hash, ct))
            return null;

        var user = await repo.GetUserAsync(record.Email, ct);
        if (user == null) return null;

        return await IssueTokensAsync(user, userAgent, ct);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Sha256Hex(refreshToken);
        await repo.DeleteRefreshTokenAsync(hash, ct);
    }

    public async Task<SecurityKey> GetValidationKeyAsync(CancellationToken ct = default)
    {
        var ec = await keyProvider.GetKeyAsync(ct);
        return new ECDsaSecurityKey(ec) { KeyId = await GetKeyIdAsync(ct) };
    }

    /// <summary>
    /// Stable key identifier — the RFC 7638 JWK thumbprint of the public key.
    /// Survives restarts (deterministic from the key material) and changes if the
    /// signing key is ever rotated.
    /// </summary>
    private async Task<string> GetKeyIdAsync(CancellationToken ct = default)
    {
        var ec = await keyProvider.GetKeyAsync(ct);
        var jwk = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(new ECDsaSecurityKey(ec));
        return Base64UrlEncoder.Encode(jwk.ComputeJwkThumbprint());
    }

    /// <summary>
    /// Returns the public half of the signing key as a JWK suitable for serving
    /// from a JWKS endpoint. The private parameters (D) are stripped.
    /// </summary>
    public async Task<JsonWebKey> GetPublicJwkAsync(CancellationToken ct = default)
    {
        var ec = await keyProvider.GetKeyAsync(ct);
        var pubParams = ec.ExportParameters(false);
        using var pubOnly = ECDsa.Create(pubParams);
        var jwk = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(new ECDsaSecurityKey(pubOnly));
        jwk.Use = "sig";
        jwk.Alg = SecurityAlgorithms.EcdsaSha256;
        jwk.KeyId = await GetKeyIdAsync(ct);
        return jwk;
    }

    public static TokenValidationParameters BuildValidationParameters(SecurityKey key)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = RolesClaim
        };
    }

    private async Task<string> CreateAccessTokenAsync(UserRecord user, CancellationToken ct)
    {
        var ec = await keyProvider.GetKeyAsync(ct);
        var key = new ECDsaSecurityKey(ec) { KeyId = await GetKeyIdAsync(ct) };
        var creds = new SigningCredentials(key, SecurityAlgorithms.EcdsaSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Email),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        // One claim per role with the same type so existing HasRoleHandler keeps working
        claims.AddRange(user.Roles.Select(role => new Claim(RolesClaim, role)));

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            claims,
            now,
            now.Add(AccessTokenLifetime),
            creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateRefreshToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncoder.Encode([.. bytes]);
    }

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}