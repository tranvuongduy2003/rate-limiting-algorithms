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

The policies under `RateLimiting:Rules` in `RateLimiting.Server/appsettings.json` are exposed through:

- `GET /api/rate-limiting-rules` to read the active policies.
- `POST /api/rate-limiting-rules/evaluate` to match and consume a policy allowance for a client.

The frontend rule cards call these endpoints directly and show the response status, remaining allowance, and retry
delay. Selecting **New client** starts with a new client identity and a fresh allowance.

## Implementing an algorithm

Fill in `AcquireAsync` in `Features/<Algorithm>/<Algorithm>Limiter.cs` and return `RateLimitDecision.Allow()`
or `RateLimitDecision.Reject(retryAfter)`. Limiters are singletons called concurrently, so keep state per client
key and make it thread-safe. Use the injected `TimeProvider` rather than `DateTime.UtcNow`, so the
algorithms can be tested with a fake clock.
