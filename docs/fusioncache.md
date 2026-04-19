# FusionCache

GAtool API uses [FusionCache](https://github.com/ZiggyCreatures/FusionCache) as the
single caching layer in front of every downstream API call (FRC, TBA, Statbotics,
FTC Scout, TOA, FIRST Global, Casterstool) and every cacheable controller action.

This document explains *why* FusionCache, *how* it is wired up, and *what
guarantees* you can rely on when adding new cached code paths.

---

## Why FusionCache

The previous implementation used a hand-rolled `[RedisCache]` action filter that
read/wrote `IDistributedCache` directly. That worked but had three problems:

1. **No stampede protection.** When a hot key expired, every concurrent request
   ran the factory at the same time — so a single popular endpoint could fan out
   into dozens of duplicate downstream calls in milliseconds. With FRC's
   strict rate limits, that is a real failure mode during big events.
2. **No fail-safe behavior.** When a downstream API blipped, every request that
   cache-missed during the outage produced a 5xx — even though we had a perfectly
   usable stale value sitting in Redis from 30 seconds ago.
3. **No coordination across ECS tasks.** Each task held its own implicit "in-flight
   factory" set, so two tasks could each independently warm the same key.

FusionCache solves all three with a small, well-scoped library:

- **Single-flight factory execution** (intra-process) — the first caller runs
  the factory, every other caller awaits the same `Task`.
- **Distributed cache (L2) + backplane** — once *any* task warms a key, every
  other task in the fleet sees it as a hit.
- **Fail-safe** — when the factory throws (e.g. FRC API 503), FusionCache
  silently returns the stale value if one is available. For a live broadcast
  tool, stale data is *always* better than a broken UI.
- **Eager refresh** — long-TTL entries refresh in the background once they
  cross 80% of their TTL, so users never wait through a real cache miss on
  popular endpoints.

---

## Architecture

```
                           ┌─────────────────────────────────────┐
                           │           ECS task (1 of N)         │
  HTTP request ──▶ Action  │  ┌──────────────────────────────┐   │
  (or service call)        │  │  L1: in-process MemoryCache  │   │
                           │  └──────────────┬───────────────┘   │
                           │                 │ miss              │
                           │                 ▼                   │
                           │  ┌──────────────────────────────┐   │
                           │  │  L2: Redis IDistributedCache │◀──┼─── shared ElastiCache
                           │  └──────────────┬───────────────┘   │    Serverless cluster
                           │                 │ miss              │
                           │                 ▼                   │
                           │  ┌──────────────────────────────┐   │
                           │  │  Factory (downstream HTTP)   │   │    rate-limited APIs
                           │  └──────────────────────────────┘   │    (FRC / TBA / …)
                           │                                     │
                           │  ┌──────────────────────────────┐   │
                           │  │  Backplane (Redis pub/sub)   │◀──┼─── notifies peer tasks
                           │  └──────────────────────────────┘   │    to evict L1
                           └─────────────────────────────────────┘
```

- **L1** = `IMemoryCache`, per-process. Sub-millisecond hits.
- **L2** = `IDistributedCache` over Redis. Single-digit-millisecond hits, shared
  across the entire fleet.
- **Backplane** = Redis pub/sub channel `FusionCache.Backplane:v2`. After any
  task writes to L2, it publishes a small invalidation message so peers drop
  their L1 entry on their next read and re-pull from L2.

### Single-flight semantics

| Scope | Guarantee | Mechanism |
|---|---|---|
| Within a single task | **Strict** — at most one factory call per key, regardless of concurrent callers | In-process `Lazy<Task<T>>` cache |
| Across the fleet | **Best-effort** — usually one factory call per cache window, but two tasks racing through a cold L1+L2 in the same few milliseconds may both run the factory | L2 lookup + write |

In production the cross-task race window is small because:
- Eager refresh (0.8 threshold) means most refreshes happen *before* TTL expiry.
- Real traffic isn't a synthetic burst — by the time the second task asks, the
  first task's factory has usually already populated L2.

This was validated locally with `scripts/test-cross-task-cache.sh` (see below).

---

## Configuration

All FusionCache wiring lives in `Program.cs` (~lines 110–180):

1. **Shared `IConnectionMultiplexer`** — single multiplexer used by both the
   distributed cache and the backplane. `AbortOnConnectFail = false` so the app
   starts even if Redis is briefly unavailable.
2. **`IDistributedCache`** — `Microsoft.Extensions.Caching.StackExchangeRedis`
   bound to the shared multiplexer.
3. **`AddFusionCache()`** with these defaults:

   | Option | Value | Why |
   |---|---|---|
   | `Duration` | 1 minute | Per-call defaults override this. |
   | `IsFailSafeEnabled` | `true` | Return stale on factory error. |
   | `FailSafeMaxDuration` | 24 hours | How long we'll keep stale values around. |
   | `FailSafeThrottleDuration` | 30s | After a failure, don't retry the factory for 30s. |
   | `FactorySoftTimeout` | 500ms | If we have stale data, give up fast and serve it. |
   | `FactoryHardTimeout` | 10s | Absolute ceiling, even with no stale data. |
   | `EagerRefreshThreshold` | 0.8 | Refresh in background at 80% of TTL. |
   | `AllowBackgroundDistributedCacheOperations` | `true` | L2 writes don't block the request. |
   | `AllowBackgroundBackplaneOperations` | `true` | Backplane publishes don't block. |

4. **Serializer** — `FusionCacheSystemTextJsonSerializer` with camelCase + case-insensitive matching, to match the rest of the codebase.
5. **Backplane** — `RedisBackplane` over the same multiplexer.

Each FusionCache call site can override individual options via a
`FusionCacheEntryOptions` argument.

---

## Two layers, one switch

Caching is **strictly opt-in** and driven by a single attribute on the
controller action: `[RedisCache(prefix, minutes)]`. When that attribute is
present it controls *both* of these layers in lock-step using the same TTL.
When it is absent, **nothing is cached** — at the response layer or downstream.

### Layer 1 — Controller response cache (`[RedisCache]`)

`Attributes/RedisCacheAttribute.cs` is an action filter that wraps the action's
final `OkObjectResult` in a FusionCache entry. The cache key is built from
`keyPrefix` + route values + query string + (for POST/PUT/PATCH) a SHA-256 hash
of the `[FromBody]` payload.

```csharp
[RedisCache("frcapi:events", RedisCacheTime.OneHour)]
[HttpGet("{year}/events")]
public async Task<IActionResult> GetEvents(int year) { ... }
```

This caches the *composed response shape* — i.e. whatever the controller
actually returns after merging downstream payloads, applying business logic,
adding extra fields, etc.

### Layer 2 — Service downstream cache (`CachedHttpGet`)

External HTTP services (`FRCApiService`, `TBAApiService`, etc.) wrap every GET
through `Services/CachedHttpGet`. The cache key is
`svc:{serviceKey}:{path-with-query}`. This is the right level for deduplication
across endpoints: even if 5 different controller actions all need the same FRC
`events` payload, they all share one cache entry and one factory call.

```csharp
public Task<T?> Get<T>(string path, IDictionary<string, string?>? query = null) =>
    CachedHttpGet.Get<T>(_cache, _ttlContext, ServiceKey, path, query, FetchTyped<T>);
```

Note there is **no `duration` argument** at the service call site. The TTL is
not threaded through method signatures — see below.

### How the two layers stay in sync

The trick is a per-request scoped class, `Services/CacheTtlContext`, that
holds a single `TimeSpan? Duration`:

1. When `[RedisCache(prefix, minutes)]` runs as an action filter, **before** it
   does its own response-cache logic it resolves `CacheTtlContext` from the
   request scope and sets `Duration = TimeSpan.FromMinutes(minutes)`.
2. When any service method calls `CachedHttpGet`, `CachedHttpGet` reads
   `CacheTtlContext.Duration`:
   - If non-null, it calls `IFusionCache.GetOrSetAsync` with that TTL.
   - If null (no `[RedisCache]` on the calling action, or the call is from a
     background job, or the action explicitly opted out), it skips
     FusionCache entirely and invokes the factory directly.
3. `RedisCache.IgnoreCurrentRequest()` from inside an action skips both layers
   — the response cache is bypassed (`SetSkipMemoryCache()` +
   `SetSkipDistributedCache(true, true)`) and `CacheTtlContext.Duration` is
   left null so subsequent service calls also bypass FusionCache.

Net effect: one `[RedisCache]` decoration on a controller action drives both
the response cache and the downstream cache with the same TTL, with no extra
code at the call sites.

### Lifetime requirements

`CacheTtlContext` is registered as **scoped**, so every request gets its own
instance and there is no cross-request leakage. As a consequence, every service
that depends on it — every `*ApiService`, plus `ScheduleService`,
`FTCScheduleService`, and `TeamDataService` — is also registered as **scoped**.
(They were singletons before; the move also fixes a latent issue where
singleton services held `HttpClient` references forever and defeated
`IHttpClientFactory`'s handler rotation.)

### Background jobs are intentionally uncached

`UpdateGlobalHighScores` and `SyncUsers` run in the same process but outside
any HTTP request scope. They get a fresh scoped `CacheTtlContext` whose
`Duration` is null, so all their downstream calls go straight to the factory.
This is the desired behavior: jobs need fresh data and shouldn't pollute or
read the request-driven cache.

---

## Cache invalidation

The admin endpoint `POST /admin/clear-redis-cache` calls both:

```csharp
await fusionCache.ClearAsync();   // L1 of every task (broadcast via backplane)
await server.FlushDatabaseAsync(); // L2
```

`ClearAsync` issues an opaque "logical" clear that, combined with the backplane,
invalidates L1 across the entire fleet — not just the task that received the
admin request.

---

## Operational characteristics

### Redis

L2 is **AWS ElastiCache Serverless (Valkey)**, single-AZ, `us-east-2`, defined
in `infra-cdk/GatoolStack.cs`. The previous Redis sidecar pattern was retired
when the fleet outgrew a single task — see `docs/cost-estimate.md` for the
infra-cost analysis that justified the move.

For local development, run a Redis container (see `LOCAL_DEVELOPMENT.md`). The
app degrades gracefully: if Redis is unreachable at startup, FusionCache
operates L1-only and re-engages L2 + backplane the moment the multiplexer
reconnects.

### Logging & metrics

FusionCache emits structured Serilog events at `Information` for factory
execution and `Warning` for fail-safe activations. In production these flow to
New Relic alongside the rest of the app's logs.

A spike in fail-safe activations is the canonical "downstream is unhappy"
signal — set up an alert on it if you want early warning of FRC/TBA/etc.
outages.

### Local validation

`scripts/test-cross-task-cache.sh` boots two API instances against a shared
Redis and runs two tests:

1. **Warm-A → read-B**: cold key, single request to task A, then single request
   to task B on the same key. Expect 2 HMSETs (one per cache layer) and 0 on
   the second call.
2. **50-way concurrent burst**: cold key, 50 concurrent requests split across
   both tasks. Expect 2–4 HMSETs total — a 12–25× reduction vs. uncached.

The script also dumps a tally of every Redis command observed via `MONITOR`,
which is the easiest way to confirm L2 + backplane are both engaged.

> Note: FusionCache stores L2 entries as Redis hashes (`HMSET` + `EXPIRE`), not
> plain `SET`s — so look for HMSETs when reading raw MONITOR output.

---

## Adding a new cached endpoint

1. **Add `[RedisCache("your:prefix", RedisCacheTime.X)]`** to the controller
   action. That single attribute is the only switch — it enables the response
   cache *and* the downstream cache for the duration of that request.
2. **Pick the TTL based on how often the underlying data changes**, not how
   often the endpoint is called. The same TTL drives both layers, so it needs
   to be safe for the freshest thing you're returning. Use the constants in
   `RedisCacheTime` (`OneMinute`, `FiveMinutes`, `OneHour`, `OneDay`,
   `ThreeDays`, `OneWeek`, `OneMonth`).
3. **If a particular response shouldn't be cached** (e.g. empty results that
   you want to retry sooner), call `RedisCache.IgnoreCurrentRequest()` from
   inside the action. That bypasses both layers for that specific request.
4. **Endpoints that should never be cached** (live scores during quals,
   admin endpoints, etc.) simply omit the `[RedisCache]` attribute. With no
   attribute there is no caching anywhere.

That's it — no per-call TTL plumbing, no L1/L2/backplane wiring, no stampede
mitigation, no fail-safe code. FusionCache handles all of it via the global
defaults, and the `[RedisCache]` attribute is the single switch that turns
both layers on.
