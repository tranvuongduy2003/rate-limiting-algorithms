using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.TokenBucket;

public sealed class TokenBucketLimiter(IOptions<TokenBucketOptions> options, TimeProvider timeProvider) : IRateLimiter
{
    private const double TokensPerRequest = 1;

    private readonly TokenBucketOptions _options = Validate(options.Value);
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();

    public ValueTask<RateLimitDecision> AcquireAsync(string clientKey, CancellationToken cancellationToken = default)
    {
        var bucket = _buckets.GetOrAdd(
            clientKey,
            static (_, state) => new Bucket(state.Capacity, state.Timestamp),
            (_options.Capacity, Timestamp: _timeProvider.GetTimestamp()));

        lock (bucket)
        {
            var now = _timeProvider.GetTimestamp();

            var elapsed = _timeProvider.GetElapsedTime(bucket.LastRefillTimestamp, now);
            bucket.Tokens = Math.Min(_options.Capacity, bucket.Tokens + elapsed.TotalSeconds * _options.RefillRatePerSecond);
            bucket.LastRefillTimestamp = now;

            if (bucket.Tokens >= TokensPerRequest)
            {
                bucket.Tokens -= TokensPerRequest;
                return ValueTask.FromResult(RateLimitDecision.Allow());
            }

            var retryAfter = TimeSpan.FromSeconds((TokensPerRequest - bucket.Tokens) / _options.RefillRatePerSecond);
            return ValueTask.FromResult(RateLimitDecision.Reject(retryAfter));
        }
    }

    private static TokenBucketOptions Validate(TokenBucketOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Capacity, 1, nameof(options.Capacity));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.RefillRatePerSecond, nameof(options.RefillRatePerSecond));
        return options;
    }

    private sealed class Bucket(double tokens, long lastRefillTimestamp)
    {
        public double Tokens { get; set; } = tokens;
        public long LastRefillTimestamp { get; set; } = lastRefillTimestamp;
    }
}
