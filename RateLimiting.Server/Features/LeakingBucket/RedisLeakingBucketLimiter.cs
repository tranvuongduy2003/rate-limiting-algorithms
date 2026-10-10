using Microsoft.Extensions.Options;
using RateLimiting.Server.Common.RateLimiting;
using StackExchange.Redis;

namespace RateLimiting.Server.Features.LeakingBucket;

public sealed class RedisLeakingBucketLimiter(
    IOptions<LeakingBucketOptions> options,
    RedisRateLimitStore store,
    TimeProvider timeProvider) : IRateLimiter
{
    private const string Algorithm = "redis-leaking-bucket";
    private const string AcquireScript = """
        local now_parts = redis.call('TIME')
        local now_ms = (tonumber(now_parts[1]) * 1000) + math.floor(tonumber(now_parts[2]) / 1000)
        local capacity = tonumber(ARGV[1])
        local leak_per_second = tonumber(ARGV[2])
        local state = redis.call('HMGET', KEYS[1], 'level', 'last_leak_ms')
        local level = tonumber(state[1]) or 0
        local last_leak_ms = tonumber(state[2]) or now_ms
        local elapsed_ms = math.max(0, now_ms - last_leak_ms)

        level = math.max(0, level - ((elapsed_ms / 1000) * leak_per_second))

        local allowed = 0
        local wait_ms
        if level + 1 <= capacity then
            wait_ms = math.ceil((level / leak_per_second) * 1000)
            level = level + 1
            allowed = 1
        else
            wait_ms = math.ceil(((level + 1 - capacity) / leak_per_second) * 1000)
        end

        redis.call('HSET', KEYS[1], 'level', level, 'last_leak_ms', now_ms)
        redis.call('PEXPIRE', KEYS[1], math.max(1000, math.ceil((capacity / leak_per_second) * 1000)))
        local remaining = math.max(0, math.floor(capacity - level))
        return { allowed, wait_ms, remaining }
        """;

    private readonly LeakingBucketOptions _options = Validate(options.Value);
    private readonly RedisRateLimitStore _store = store;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async ValueTask<RateLimitDecision> AcquireAsync(
        string clientKey,
        CancellationToken cancellationToken = default)
    {
        var result = await _store.EvaluateAsync(
            AcquireScript,
            RedisRateLimitStore.CreateKey(Algorithm, clientKey),
            [_options.Capacity, _options.LeakRatePerSecond],
            cancellationToken);

        if (result is null)
        {
            return RateLimitDecision.Allow(_options.Capacity, _options.Capacity);
        }

        var wait = TimeSpan.FromMilliseconds((long)result[1]);
        if ((long)result[0] == 0)
        {
            return RateLimitDecision.Reject(_options.Capacity, wait);
        }

        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, _timeProvider, cancellationToken);
        }

        return RateLimitDecision.Allow(_options.Capacity, (int)(long)result[2]);
    }

    private static LeakingBucketOptions Validate(LeakingBucketOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Capacity, 1, nameof(options.Capacity));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.LeakRatePerSecond, nameof(options.LeakRatePerSecond));
        return options;
    }
}
