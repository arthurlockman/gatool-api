namespace GAToolAPI.Services;

/// <summary>
///     Per-request scoped holder for the desired downstream cache TTL.
///     Populated by <see cref="GAToolAPI.Attributes.RedisCacheAttribute" /> at the start of an
///     action so any <see cref="CachedHttpGet" /> calls made by services during that request
///     are cached for the matching duration.
/// </summary>
/// <remarks>
///     If <see cref="Duration" /> is <c>null</c> (e.g. a request to an action without
///     <see cref="GAToolAPI.Attributes.RedisCacheAttribute" />, or a background job), downstream
///     calls bypass FusionCache entirely and execute against the upstream API directly.
///     Caching is therefore strictly opt-in via the controller attribute.
/// </remarks>
public class CacheTtlContext
{
    public TimeSpan? Duration { get; set; }
}
