using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.SlidingWindowCounter;

public sealed class SlidingWindowCounterLimiter(IOptions<SlidingWindowCounterOptions> options, TimeProvider timeProvider)
    : IRateLimiter
{
    private readonly SlidingWindowCounterOptions _options = Validate(options.Value);
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ConcurrentDictionary<string, WindowCounter> _counters = new();

    public ValueTask<RateLimitDecision> AcquireAsync(string clientKey, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var windowNumber = now.UtcTicks / _options.Window.Ticks;
        var counter = _counters.GetOrAdd(clientKey, static (_, number) => new WindowCounter(number), windowNumber);

        lock (counter)
        {
            AdvanceWindow(counter, windowNumber);

            var elapsedWindowTicks = now.UtcTicks % _options.Window.Ticks;
            var remainingWindowTicks = _options.Window.Ticks - elapsedWindowTicks;
            var previousWindowWeight = (double)remainingWindowTicks / _options.Window.Ticks;
            var estimatedCount = counter.CurrentCount + (counter.PreviousCount * previousWindowWeight);

            if (estimatedCount + 1 <= _options.Limit)
            {
                counter.CurrentCount++;
                var remaining = (int)Math.Floor(_options.Limit - estimatedCount - 1);
                return ValueTask.FromResult(RateLimitDecision.Allow(_options.Limit, remaining));
            }

            var retryAfter = CalculateRetryAfter(counter, remainingWindowTicks);
            return ValueTask.FromResult(RateLimitDecision.Reject(_options.Limit, retryAfter));
        }
    }

    private void AdvanceWindow(WindowCounter counter, long windowNumber)
    {
        if (counter.WindowNumber == windowNumber)
        {
            return;
        }

        counter.PreviousCount = windowNumber == counter.WindowNumber + 1
            ? counter.CurrentCount
            : 0;
        counter.CurrentCount = 0;
        counter.WindowNumber = windowNumber;
    }

    private TimeSpan CalculateRetryAfter(WindowCounter counter, long remainingWindowTicks)
    {
        var availableForPreviousWindow = _options.Limit - counter.CurrentCount - 1;

        if (availableForPreviousWindow >= 0 && counter.PreviousCount > 0)
        {
            var targetRemainingTicks =
                (double)availableForPreviousWindow * _options.Window.Ticks / counter.PreviousCount;
            var retryTicks = Math.Ceiling(remainingWindowTicks - targetRemainingTicks);
            return TimeSpan.FromTicks(Math.Max(1, (long)retryTicks));
        }

        // The current window is already full. At the next boundary it becomes
        // the previous window and must decay enough to make room for a request.
        var ticksAfterBoundary = Math.Ceiling((double)_options.Window.Ticks / counter.CurrentCount);
        return TimeSpan.FromTicks(remainingWindowTicks + Math.Max(1, (long)ticksAfterBoundary));
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

    private sealed class WindowCounter(long windowNumber)
    {
        public long WindowNumber { get; set; } = windowNumber;
        public int PreviousCount { get; set; }
        public int CurrentCount { get; set; }
    }
}
