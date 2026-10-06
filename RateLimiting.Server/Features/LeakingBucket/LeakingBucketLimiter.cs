using Microsoft.Extensions.Options;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.LeakingBucket;

public sealed class LeakingBucketLimiter(IOptions<LeakingBucketOptions> options, TimeProvider timeProvider) : IRateLimiter
{
    private readonly LeakingBucketOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;

    public ValueTask<RateLimitDecision> AcquireAsync(string clientKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
