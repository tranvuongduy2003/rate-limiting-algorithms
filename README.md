# Rate limiting algorithms

Rate limiting algorithms implemented from scratch in a .NET 10 Minimal API, with a React frontend to try
them out and Aspire to run both.

## Run

```bash
aspire run
```

Open the `webfrontend` link in the Aspire dashboard. To run the API alone, use
`dotnet run --project RateLimiting.Server` and
[RateLimiting.Server.http](RateLimiting.Server/RateLimiting.Server.http).

## Layout

The server is organized by vertical slices: one folder per algorithm, holding everything that algorithm needs.

```text
RateLimiting.Server/
  Common/
    Extensions.cs                     Aspire service defaults
    AlgorithmResponse.cs
    RateLimiting/
      IRateLimiter.cs                 contract every algorithm implements
      RateLimitDecision.cs
      RateLimiterEndpointFilter.cs    calls the limiter; returns 429 + Retry-After on rejection
      RateLimiterEndpointExtensions.cs  .RequireRateLimiter<TLimiter>()
  Features/
    TokenBucket/                      TokenBucketOptions, TokenBucketLimiter (TODO), TokenBucketEndpoint
    LeakingBucket/                    ...
    FixedWindowCounter/
    SlidingWindowLog/
    SlidingWindowCounter/

frontend/src/
  shared/                             API helper and the generic AlgorithmCard
  features/<algorithm>/               one component per algorithm
```

| Endpoint | Algorithm | Settings (`RateLimiting` section of `appsettings.json`) |
| --- | --- | --- |
| `GET /api/token-bucket` | Token bucket | `Capacity`, `RefillRatePerSecond` |
| `GET /api/leaking-bucket` | Leaking bucket | `Capacity`, `LeakRatePerSecond` |
| `GET /api/fixed-window-counter` | Fixed window counter | `Limit`, `Window` |
| `GET /api/sliding-window-log` | Sliding window log | `Limit`, `Window` |
| `GET /api/sliding-window-counter` | Sliding window counter | `Limit`, `Window` |

Requests are limited per client: the `X-Client-Id` header if present, otherwise the remote IP.

Every limited endpoint returns `X-RateLimit-Limit` and `X-RateLimit-Remaining`. Rejected requests also return
`429 Too Many Requests`, `Retry-After`, and `X-RateLimit-Retry-After`.

## Configured rules

Configured rate limiting now follows the complete distributed request path:

1. `RateLimiting.Server/rate-limit-rules.json` is the disk rule store.
2. `RateLimitingRuleRefreshWorker` loads it before the server accepts traffic, then refreshes an atomically replaced
   in-memory snapshot on the configured interval. A bad refresh leaves the last valid snapshot active.
3. `RateLimitingRuleMiddleware` matches protected endpoint metadata against that cache.
4. `ConfiguredRateLimiter` atomically updates the per-rule, per-client counter and last-request timestamp in Redis.
5. Allowed requests continue to the API handler. Rejected requests receive `429 Too Many Requests` and are either
   dropped or written to a bounded Redis stream.

The architecture settings live under `RateLimiting:Rules` in `RateLimiting.Server/appsettings.json`:

| Setting | Purpose |
| --- | --- |
| `FilePath` | Rule document, resolved relative to the server content root |
| `RefreshInterval` | How often each server worker reloads the file |
| `RejectedRequestBehavior` | `Drop` or `Queue` |
| `QueueKey` | Redis stream used when behavior is `Queue` |
| `QueueMaxLength` | Approximate bounded stream length |
| `QueuedBodyMaxBytes` | Maximum request body copied into a queue message |

Protected endpoints opt in with `.RequireConfiguredRateLimit(domain, descriptorKey, descriptorValue)`. Two complete
examples are included:

- `POST /api/messages/marketing`
- `POST /api/auth/login`

Operational/demo endpoints:

- `GET /api/rate-limiting-rules` to read the active policies.
- `POST /api/rate-limiting-rules/evaluate` to match and consume a policy allowance for a client.
- `GET /api/rate-limiting-rules/queue` to inspect the configured rejection behavior and Redis stream length.

The frontend rule cards call these endpoints directly and show the response status, remaining allowance, and retry
delay. Selecting **New client** starts with a new client identity and a fresh allowance.

Redis outages are fail-open for rate checks so the limiter cannot take the API down. The failure is logged and the
request proceeds. Queue failures are also logged; the client still receives the original 429 response.

## Implementing an algorithm

Fill in `AcquireAsync` in `Features/<Algorithm>/<Algorithm>Limiter.cs` and return `RateLimitDecision.Allow()`
or `RateLimitDecision.Reject(retryAfter)`. Limiters are singletons called concurrently, so keep state per client
key and make it thread-safe. Use the injected `TimeProvider` rather than `DateTime.UtcNow`, so the
algorithms can be tested with a fake clock.
