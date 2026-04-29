using StackExchange.Redis;

namespace GAToolAPI.Services.Auth;

/// <summary>
/// Distributed fixed-window rate limiter backed by Redis INCR + EXPIRE.
///
/// Each (bucket, key) pair maps to a counter that starts at 0 and resets when its
/// TTL elapses. The first request in a window sets the TTL; subsequent requests
/// only increment. If Redis is unreachable we fail open and log a warning — the
/// caller's flow (e.g. issuing an OTP) is more important than a perfect rate cap.
/// </summary>
public class RedisRateLimiter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRateLimiter> _logger;

    public RedisRateLimiter(IConnectionMultiplexer redis, ILogger<RedisRateLimiter> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    /// <summary>
    /// Returns true if the call is permitted, false if the limit has been exceeded.
    /// </summary>
    public async Task<bool> TryAcquireAsync(string bucket, string key, int limit, TimeSpan window)
    {
        var redisKey = $"ratelimit:{bucket}:{key}";
        try
        {
            var db = _redis.GetDatabase();
            var count = await db.StringIncrementAsync(redisKey);
            if (count == 1)
            {
                // First hit in this window — set the TTL. Use KeyExpire so we don't
                // race with a concurrent INCR resetting the value.
                await db.KeyExpireAsync(redisKey, window, ExpireWhen.HasNoExpiry);
            }
            return count <= limit;
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis rate limiter unavailable for {Bucket}:{Key} — failing open", bucket, key);
            return true;
        }
    }
}
