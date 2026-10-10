using Microsoft.Extensions.Options;
using RateLimiting.Server.Common.RateLimiting;
using StackExchange.Redis;

namespace RateLimiting.Server.Features.FixedWindowCounter;

public sealed class RedisFixedWindowCounterLimiter(
    IOptions<FixedWindowCounterOptions> options,
    RedisRateLimitStore store) : IRateLimiter
{
    private const string Algorithm = "redis-fixed-window-counter";
    private const string AcquireScript = """
        local now_parts = redis.call('TIME')
        local now_ms = (tonumber(now_parts[1]) * 1000) + math.floor(tonumber(now_parts[2]) / 1000)
        local window_ms = tonumber(ARGV[1])
        local request_limit = tonumber(ARGV[2])
        local window_number = math.floor(now_ms / window_ms)
        local stored_window = tonumber(redis.call('HGET', KEYS[1], 'window'))

        if stored_window ~= window_number then
            redis.call('DEL', KEYS[1])
            redis.call('HSET', KEYS[1], 'window', window_number, 'count', 0)
        end

        local count = tonumber(redis.call('HGET', KEYS[1], 'count')) or 0
        local allowed = 0
        if count < request_limit then
            redis.call('HINCRBY', KEYS[1], 'count', 1)
            count = count + 1
            allowed = 1
        end

        local retry_after_ms = window_ms - (now_ms % window_ms)
        redis.call('PEXPIRE', KEYS[1], retry_after_ms)
        local remaining = math.max(0, request_limit - count)
        return { allowed, retry_after_ms, remaining }
        """;

    private readonly FixedWindowCounterOptions _options = Validate(options.Value);
    private readonly RedisRateLimitStore _store = store;

    public async ValueTask<RateLimitDecision> AcquireAsync(
        string clientKey,
        CancellationToken cancellationToken = default)
    {
        var result = await _store.EvaluateAsync(
            AcquireScript,
            RedisRateLimitStore.CreateKey(Algorithm, clientKey),
            [(long)Math.Ceiling(_options.Window.TotalMilliseconds), _options.Limit],
            cancellationToken);

        if (result is null)
        {
            return RateLimitDecision.Allow(_options.Limit, _options.Limit);
        }

        return (long)result[0] == 1
            ? RateLimitDecision.Allow(_options.Limit, (int)(long)result[2])
            : RateLimitDecision.Reject(_options.Limit, TimeSpan.FromMilliseconds((long)result[1]));
    }

    private static FixedWindowCounterOptions Validate(FixedWindowCounterOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Limit, 1, nameof(options.Limit));
        if (options.Window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Window), "Window must be greater than zero.");
        }

        return options;
    }
}
