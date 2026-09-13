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
public class OtpService(
    AuthRepository repo,
    AuthEmailService email,
    OtpPepperProvider pepper,
    RedisRateLimiter rateLimiter,
    ILogger<OtpService> logger)
{
    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);
    public const int MaxVerifyAttempts = 5;
    private const int CodeLength = 6;
    private const int IssueLimitPerWindow = 3;
    private static readonly TimeSpan IssueWindow = TimeSpan.FromMinutes(5);

    public enum IssueResult
    {
        Sent,
        RateLimited,
        EmailFailed
    }

    public async Task<IssueResult> IssueAsync(string email1, CancellationToken ct = default)
    {
        var normalized = email1.Trim().ToLowerInvariant();
        if (!await rateLimiter.TryAcquireAsync("otp-issue", normalized, IssueLimitPerWindow, IssueWindow))
        {
            logger.LogInformation("Rate limited OTP request for {Email}", normalized);
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
        await repo.SaveOtpAsync(record, ct);

        try
        {
            await email.SendOtpAsync(normalized, code, OtpLifetime, ct);
            return IssueResult.Sent;
        }
        catch
        {
            // Email failed — clean up the unsendable code so the user isn't locked out
            await repo.DeleteOtpAsync(normalized, ct);
            return IssueResult.EmailFailed;
        }
    }

    public enum VerifyResult
    {
        Ok,
        NotFound,
        Expired,
        InvalidCode,
        NoAttemptsLeft
    }

    /// <summary>
    /// Verify a submitted code. On success the OTP is atomically consumed (deleted under
    /// a condition that the codeHash still matches) so concurrent verifications can't
    /// double-redeem. On failure attempts is decremented; when attempts hit 0 the code
    /// is destroyed.
    /// </summary>
    public async Task<VerifyResult> VerifyAsync(string email, string code, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var record = await repo.GetOtpAsync(normalized, ct);
        if (record == null) return VerifyResult.NotFound;
        if (record.ExpiresAt < DateTimeOffset.UtcNow) return VerifyResult.Expired;
        if (record.AttemptsRemaining <= 0)
        {
            await repo.DeleteOtpAsync(normalized, ct);
            return VerifyResult.NoAttemptsLeft;
        }

        var submittedHash = await HashAsync(code.Trim(), ct);
        // Constant-time comparison
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(submittedHash),
                System.Text.Encoding.ASCII.GetBytes(record.CodeHash)))
        {
            await repo.DecrementOtpAttemptsAsync(normalized, ct);
            return VerifyResult.InvalidCode;
        }

        // Conditional delete: only consume if the code we hashed matches what's still stored.
        // Prevents a concurrent successful verification from double-spending the same code.
        if (!await repo.TryConsumeOtpAsync(normalized, record.CodeHash, ct))
            return VerifyResult.NotFound;

        return VerifyResult.Ok;
    }

    private async Task<string> HashAsync(string code, CancellationToken ct)
    {
        var pepper1 = await pepper.GetAsync(ct);
        var hash = HMACSHA256.HashData(pepper1, System.Text.Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GenerateCode()
    {
        // Uniformly distributed 6-digit code. RandomNumberGenerator.GetInt32 is unbiased.
        var n = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return n.ToString("D" + CodeLength);
    }
}