using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.LeakingBucket;

public sealed class LeakingBucketLimiter(IOptions<LeakingBucketOptions> options, TimeProvider timeProvider) : IRateLimiter
{
    private const double WaterPerRequest = 1;

    private readonly LeakingBucketOptions _options = Validate(options.Value);
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();

    public async ValueTask<RateLimitDecision> AcquireAsync(string clientKey, CancellationToken cancellationToken = default)
    {
        var bucket = _buckets.GetOrAdd(
            clientKey,
            static (_, timestamp) => new Bucket(0, timestamp),
            _timeProvider.GetTimestamp());

        TimeSpan queueDelay;
        lock (bucket)
        {
            var now = _timeProvider.GetTimestamp();

            var elapsed = _timeProvider.GetElapsedTime(bucket.LastLeakTimestamp, now);
            bucket.Level = Math.Max(0, bucket.Level - elapsed.TotalSeconds * _options.LeakRatePerSecond);
            bucket.LastLeakTimestamp = now;

            if (bucket.Level + WaterPerRequest > _options.Capacity)
            {
                var retryAfter = TimeSpan.FromSeconds((bucket.Level + WaterPerRequest - _options.Capacity) / _options.LeakRatePerSecond);
                return RateLimitDecision.Reject(retryAfter);
            }

            queueDelay = TimeSpan.FromSeconds(bucket.Level / _options.LeakRatePerSecond);
            bucket.Level += WaterPerRequest;
        }

        if (queueDelay > TimeSpan.Zero)
        {
            await Task.Delay(queueDelay, _timeProvider, cancellationToken);
        }

        return RateLimitDecision.Allow();
    }

    private static LeakingBucketOptions Validate(LeakingBucketOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Capacity, 1, nameof(options.Capacity));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.LeakRatePerSecond, nameof(options.LeakRatePerSecond));
        return options;
    }

    private sealed class Bucket(double level, long lastLeakTimestamp)
    {
        public double Level { get; set; } = level;
        public long LastLeakTimestamp { get; set; } = lastLeakTimestamp;
    }
}
