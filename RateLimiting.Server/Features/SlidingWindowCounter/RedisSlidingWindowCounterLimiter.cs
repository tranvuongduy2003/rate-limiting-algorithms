using Microsoft.Extensions.Options;
using RateLimiting.Server.Common.RateLimiting;
using StackExchange.Redis;

namespace RateLimiting.Server.Features.SlidingWindowCounter;

public sealed class RedisSlidingWindowCounterLimiter(
    IOptions<SlidingWindowCounterOptions> options,
    RedisRateLimitStore store) : IRateLimiter
{
    private const string Algorithm = "redis-sliding-window-counter";
    private const string AcquireScript = """
        local now_parts = redis.call('TIME')
        local now_ms = (tonumber(now_parts[1]) * 1000) + math.floor(tonumber(now_parts[2]) / 1000)
        local window_ms = tonumber(ARGV[1])
        local request_limit = tonumber(ARGV[2])
        local window_number = math.floor(now_ms / window_ms)
        local state = redis.call('HMGET', KEYS[1], 'window', 'previous', 'current')
        local stored_window = tonumber(state[1])
        local previous_count = tonumber(state[2]) or 0
        local current_count = tonumber(state[3]) or 0

        if stored_window == nil then
            stored_window = window_number
        elseif stored_window ~= window_number then
            if window_number == stored_window + 1 then
                previous_count = current_count
            else
                previous_count = 0
            end
            current_count = 0
            stored_window = window_number
        end

        local elapsed_window_ms = now_ms % window_ms
        local remaining_window_ms = window_ms - elapsed_window_ms
        local previous_weight = remaining_window_ms / window_ms
        local estimated_count = current_count + (previous_count * previous_weight)
        local allowed = 0
        local retry_after_ms = 0

        if estimated_count + 1 <= request_limit then
            current_count = current_count + 1
            allowed = 1
        else
            local available_for_previous = request_limit - current_count - 1
            if available_for_previous >= 0 and previous_count > 0 then
                local target_remaining_ms = (available_for_previous * window_ms) / previous_count
                retry_after_ms = math.max(1, math.ceil(remaining_window_ms - target_remaining_ms))
            else
                retry_after_ms = remaining_window_ms + math.max(1, math.ceil(window_ms / current_count))
            end
        end

        redis.call('HSET', KEYS[1],
            'window', stored_window,
            'previous', previous_count,
            'current', current_count)
        redis.call('PEXPIRE', KEYS[1], window_ms * 2)
        local remaining = math.max(0, math.floor(request_limit - estimated_count - allowed))
        return { allowed, retry_after_ms, remaining }
        """;

    private readonly SlidingWindowCounterOptions _options = Validate(options.Value);
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

    private static SlidingWindowCounterOptions Validate(SlidingWindowCounterOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Limit, 1, nameof(options.Limit));
        if (options.Window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Window), "Window must be greater than zero.");
        }

        return options;
    }
}
