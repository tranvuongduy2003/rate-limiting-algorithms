using Microsoft.Extensions.Options;
using RateLimiting.Server.Common.RateLimiting;
using StackExchange.Redis;

namespace RateLimiting.Server.Features.TokenBucket;

public sealed class RedisTokenBucketLimiter(
    IOptions<TokenBucketOptions> options,
    RedisRateLimitStore store) : IRateLimiter
{
    private const string Algorithm = "redis-token-bucket";
    private const string AcquireScript = """
        local now_parts = redis.call('TIME')
        local now_ms = (tonumber(now_parts[1]) * 1000) + math.floor(tonumber(now_parts[2]) / 1000)
        local capacity = tonumber(ARGV[1])
        local refill_per_second = tonumber(ARGV[2])
        local state = redis.call('HMGET', KEYS[1], 'tokens', 'last_refill_ms')
        local tokens = tonumber(state[1]) or capacity
        local last_refill_ms = tonumber(state[2]) or now_ms
        local elapsed_ms = math.max(0, now_ms - last_refill_ms)

        tokens = math.min(capacity, tokens + ((elapsed_ms / 1000) * refill_per_second))

        local allowed = 0
        local retry_after_ms = 0
        if tokens >= 1 then
            tokens = tokens - 1
            allowed = 1
        else
            retry_after_ms = math.ceil(((1 - tokens) / refill_per_second) * 1000)
        end

        redis.call('HSET', KEYS[1], 'tokens', tokens, 'last_refill_ms', now_ms)
        redis.call('PEXPIRE', KEYS[1], math.max(1000, math.ceil((capacity / refill_per_second) * 1000)))
        return { allowed, retry_after_ms }
        """;

    private readonly TokenBucketOptions _options = Validate(options.Value);
    private readonly RedisRateLimitStore _store = store;

    public async ValueTask<RateLimitDecision> AcquireAsync(
        string clientKey,
        CancellationToken cancellationToken = default)
    {
        var result = await _store.EvaluateAsync(
            AcquireScript,
            RedisRateLimitStore.CreateKey(Algorithm, clientKey),
            [_options.Capacity, _options.RefillRatePerSecond],
            cancellationToken);

        return result is null || (long)result[0] == 1
            ? RateLimitDecision.Allow()
            : RateLimitDecision.Reject(TimeSpan.FromMilliseconds((long)result[1]));
    }

    private static TokenBucketOptions Validate(TokenBucketOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Capacity, 1, nameof(options.Capacity));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.RefillRatePerSecond, nameof(options.RefillRatePerSecond));
        return options;
    }
}
