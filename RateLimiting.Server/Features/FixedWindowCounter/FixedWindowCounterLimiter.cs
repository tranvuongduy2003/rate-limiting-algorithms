using Microsoft.Extensions.Options;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.FixedWindowCounter;

public sealed class FixedWindowCounterLimiter(IOptions<FixedWindowCounterOptions> options, TimeProvider timeProvider)
    : IRateLimiter
{
    private readonly FixedWindowCounterOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;

    public ValueTask<RateLimitDecision> AcquireAsync(string clientKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
