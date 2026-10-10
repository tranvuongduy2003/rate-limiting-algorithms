using Microsoft.Extensions.Options;
using RateLimiting.Server.Common.RateLimiting;
using StackExchange.Redis;

namespace RateLimiting.Server.Features.SlidingWindowLog;

public sealed class RedisSlidingWindowLogLimiter(
    IOptions<SlidingWindowLogOptions> options,
    RedisRateLimitStore store) : IRateLimiter
{
    private const string Algorithm = "redis-sliding-window-log";
    private const string AcquireScript = """
        local now_parts = redis.call('TIME')
        local now_ms = (tonumber(now_parts[1]) * 1000) + math.floor(tonumber(now_parts[2]) / 1000)
        local window_ms = tonumber(ARGV[1])
        local request_limit = tonumber(ARGV[2])
        local window_start_ms = now_ms - window_ms

        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', window_start_ms)
        local count = redis.call('ZCARD', KEYS[1])

        if count < request_limit then
            local member = tostring(now_ms) .. ':' .. tostring(count + 1)
            redis.call('ZADD', KEYS[1], now_ms, member)
            count = count + 1
            redis.call('PEXPIRE', KEYS[1], window_ms)
            return { 1, 0, request_limit - count }
        end

        local retry_entry = redis.call('ZRANGE', KEYS[1], 0, 0, 'WITHSCORES')
        local retry_after_ms = math.max(1, tonumber(retry_entry[2]) + window_ms - now_ms)
        return { 0, retry_after_ms, 0 }
        """;

    private readonly SlidingWindowLogOptions _options = Validate(options.Value);
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

    private static SlidingWindowLogOptions Validate(SlidingWindowLogOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Limit, 1, nameof(options.Limit));
        if (options.Window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Window), "Window must be greater than zero.");
        }

        return options;
    }
}
