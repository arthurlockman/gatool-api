using System.Collections.Concurrent;

namespace GAToolAPI.Helpers;

/// <summary>
///     Helpers for fanning out per-item work in batch query endpoints with a bounded
///     degree of parallelism. Replaces the unbounded <c>Task.WhenAll(items.Select(...))</c>
///     pattern that could spawn hundreds of concurrent downstream calls (e.g. ~945
///     parallel FTC API calls for a 315-team event), driving CPU spikes on the API
///     containers and pushing aggressively against upstream rate limits.
///
///     Concurrency is sourced from <c>BatchQuery:MaxConcurrency</c> via
///     <see cref="BatchMaxConcurrency"/>. Per-item helpers are expected to swallow
///     their own exceptions (all current call sites do); any exception that does
///     escape will fault the whole batch, matching the prior <c>Task.WhenAll</c>
///     behavior.
/// </summary>
public static class BatchExecutionExtensions
{
    private const int DefaultMaxConcurrency = 25;

    /// <summary>
    ///     Reads <c>BatchQuery:MaxConcurrency</c> from configuration, defaulting to 25.
    /// </summary>
    public static int BatchMaxConcurrency(this IConfiguration configuration) =>
        configuration.GetValue("BatchQuery:MaxConcurrency", DefaultMaxConcurrency);

    /// <summary>
    ///     Runs <paramref name="selector"/> against each key with at most
    ///     <paramref name="maxConcurrency"/> calls in flight, returning a
    ///     dictionary keyed by the input. Duplicate keys keep the last result.
    /// </summary>
    public static async Task<Dictionary<TKey, TValue>> BatchToDictionaryAsync<TKey, TValue>(
        this IEnumerable<TKey> keys,
        Func<TKey, Task<TValue>> selector,
        int maxConcurrency,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        var results = new ConcurrentDictionary<TKey, TValue>();
        await Parallel.ForEachAsync(
            keys,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, maxConcurrency),
                CancellationToken = cancellationToken
            },
            async (key, _) => results[key] = await selector(key));
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    ///     Parallel async map with a bounded degree of parallelism — like
    ///     <c>Enumerable.Select</c> + <c>Task.WhenAll</c>, but capped. Output order
    ///     matches input order.
    /// </summary>
    public static async Task<TResult[]> BatchSelectAsync<TItem, TResult>(
        this IEnumerable<TItem> items,
        Func<TItem, Task<TResult>> selector,
        int maxConcurrency,
        CancellationToken cancellationToken = default)
    {
        var inputs = items as IList<TItem> ?? items.ToList();
        var results = new TResult[inputs.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, inputs.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, maxConcurrency),
                CancellationToken = cancellationToken
            },
            async (i, _) => results[i] = await selector(inputs[i]));
        return results;
    }
}
