using Microsoft.Extensions.Options;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.SlidingWindowLog;

public sealed class SlidingWindowLogLimiter(IOptions<SlidingWindowLogOptions> options, TimeProvider timeProvider)
    : IRateLimiter
{
    private readonly SlidingWindowLogOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;

    public ValueTask<RateLimitDecision> AcquireAsync(string clientKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
