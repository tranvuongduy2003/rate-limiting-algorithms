using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.FixedWindowCounter;

public sealed class FixedWindowCounterLimiter(IOptions<FixedWindowCounterOptions> options, TimeProvider timeProvider)
    : IRateLimiter
{
    private readonly FixedWindowCounterOptions _options = Validate(options.Value);
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ConcurrentDictionary<string, WindowCounter> _counters = new();

    public ValueTask<RateLimitDecision> AcquireAsync(string clientKey, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var windowNumber = now.UtcTicks / _options.Window.Ticks;
        var counter = _counters.GetOrAdd(clientKey, static (_, number) => new WindowCounter(number), windowNumber);

        lock (counter)
        {
            if (counter.WindowNumber != windowNumber)
            {
                counter.WindowNumber = windowNumber;
                counter.Count = 0;
            }

            if (counter.Count < _options.Limit)
            {
                counter.Count++;
                return ValueTask.FromResult(RateLimitDecision.Allow());
            }

            var elapsedWindowTicks = now.UtcTicks % _options.Window.Ticks;
            var retryAfter = TimeSpan.FromTicks(_options.Window.Ticks - elapsedWindowTicks);
            return ValueTask.FromResult(RateLimitDecision.Reject(retryAfter));
        }
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

    private sealed class WindowCounter(long windowNumber)
    {
        public long WindowNumber { get; set; } = windowNumber;
        public int Count { get; set; }
    }
}
