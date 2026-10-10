using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.SlidingWindowLog;

public sealed class SlidingWindowLogLimiter(IOptions<SlidingWindowLogOptions> options, TimeProvider timeProvider)
    : IRateLimiter
{
    private readonly SlidingWindowLogOptions _options = Validate(options.Value);
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _requestLogs = new();

    public ValueTask<RateLimitDecision> AcquireAsync(string clientKey, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var windowStart = now - _options.Window;
        var requestLog = _requestLogs.GetOrAdd(clientKey, static _ => new Queue<DateTimeOffset>());

        lock (requestLog)
        {
            while (requestLog.TryPeek(out var timestamp) && timestamp <= windowStart)
            {
                requestLog.Dequeue();
            }

            if (requestLog.Count < _options.Limit)
            {
                requestLog.Enqueue(now);
                return ValueTask.FromResult(RateLimitDecision.Allow(_options.Limit, _options.Limit - requestLog.Count));
            }

            var retryTimestamp = requestLog.Peek();
            var retryAfter = retryTimestamp + _options.Window - now;
            return ValueTask.FromResult(RateLimitDecision.Reject(_options.Limit, retryAfter));
        }
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
