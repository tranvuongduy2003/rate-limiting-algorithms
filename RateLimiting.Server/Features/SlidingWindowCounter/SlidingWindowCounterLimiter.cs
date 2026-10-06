using Microsoft.Extensions.Options;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.SlidingWindowCounter;

public sealed class SlidingWindowCounterLimiter(IOptions<SlidingWindowCounterOptions> options, TimeProvider timeProvider)
    : IRateLimiter
{
    private readonly SlidingWindowCounterOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;

    public ValueTask<RateLimitDecision> AcquireAsync(string clientKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
