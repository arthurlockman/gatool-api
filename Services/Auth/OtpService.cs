using System.Security.Cryptography;
using GAToolAPI.Models;

namespace GAToolAPI.Services.Auth;

/// <summary>
///     Generates and verifies one-time email login codes.
///
///     - Code: 6-digit numeric (uniformly random)
///     - TTL: 10 minutes
///     - Verification attempts: 5 (then code is destroyed)
///     - Generation rate limit: 3 codes per email per 5 minutes (Redis fixed-window, fleet-wide)
///     - Stored hash: HMAC-SHA256(pepper, code) — pepper is in Secrets Manager,
///       so DynamoDB read access alone cannot brute-force a 6-digit code offline.
/// </summary>
public class OtpService
{
    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);
    public const int MaxVerifyAttempts = 5;
    private const int CodeLength = 6;
    private const int IssueLimitPerWindow = 3;
    private static readonly TimeSpan IssueWindow = TimeSpan.FromMinutes(5);

    private readonly AuthRepository _repo;
    private readonly AuthEmailService _email;
    private readonly OtpPepperProvider _pepper;
    private readonly RedisRateLimiter _rateLimiter;
    private readonly ILogger<OtpService> _logger;

    public OtpService(AuthRepository repo, AuthEmailService email,
        OtpPepperProvider pepper, RedisRateLimiter rateLimiter, ILogger<OtpService> logger)
    {
        _repo = repo;
        _email = email;
        _pepper = pepper;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public enum IssueResult { Sent, RateLimited, EmailFailed }

    public async Task<IssueResult> IssueAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (!await _rateLimiter.TryAcquireAsync("otp-issue", normalized, IssueLimitPerWindow, IssueWindow))
        {
            _logger.LogInformation("Rate limited OTP request for {Email}", normalized);
            return IssueResult.RateLimited;
        }

        var code = GenerateCode();
        var record = new OtpRecord
        {
            Email = normalized,
            CodeHash = await HashAsync(code, ct),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(OtpLifetime),
            AttemptsRemaining = MaxVerifyAttempts
        };
        await _repo.SaveOtpAsync(record, ct);

        try
        {
            await _email.SendOtpAsync(normalized, code, OtpLifetime, ct);
            return IssueResult.Sent;
        }
        catch
        {
            // Email failed — clean up the unsendable code so the user isn't locked out
            await _repo.DeleteOtpAsync(normalized, ct);
            return IssueResult.EmailFailed;
        }
    }

    public enum VerifyResult { Ok, NotFound, Expired, InvalidCode, NoAttemptsLeft }

    /// <summary>
    /// Verify a submitted code. On success the OTP is atomically consumed (deleted under
    /// a condition that the codeHash still matches) so concurrent verifications can't
    /// double-redeem. On failure attempts is decremented; when attempts hit 0 the code
    /// is destroyed.
    /// </summary>
    public async Task<VerifyResult> VerifyAsync(string email, string code, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var record = await _repo.GetOtpAsync(normalized, ct);
        if (record == null) return VerifyResult.NotFound;
        if (record.ExpiresAt < DateTimeOffset.UtcNow) return VerifyResult.Expired;
        if (record.AttemptsRemaining <= 0)
        {
            await _repo.DeleteOtpAsync(normalized, ct);
            return VerifyResult.NoAttemptsLeft;
        }

        var submittedHash = await HashAsync(code.Trim(), ct);
        // Constant-time comparison
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(submittedHash),
                System.Text.Encoding.ASCII.GetBytes(record.CodeHash)))
        {
            await _repo.DecrementOtpAttemptsAsync(normalized, ct);
            return VerifyResult.InvalidCode;
        }

        // Conditional delete: only consume if the code we hashed matches what's still stored.
        // Prevents a concurrent successful verification from double-spending the same code.
        if (!await _repo.TryConsumeOtpAsync(normalized, record.CodeHash, ct))
            return VerifyResult.NotFound;

        return VerifyResult.Ok;
    }

    private async Task<string> HashAsync(string code, CancellationToken ct)
    {
        var pepper = await _pepper.GetAsync(ct);
        var hash = HMACSHA256.HashData(pepper, System.Text.Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GenerateCode()
    {
        // Uniformly distributed 6-digit code. RandomNumberGenerator.GetInt32 is unbiased.
        var n = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return n.ToString("D" + CodeLength);
    }
}
