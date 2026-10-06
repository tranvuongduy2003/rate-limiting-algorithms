using Microsoft.Extensions.Options;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.TokenBucket;

public sealed class TokenBucketLimiter(IOptions<TokenBucketOptions> options, TimeProvider timeProvider) : IRateLimiter
{
    private readonly TokenBucketOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;

    public ValueTask<RateLimitDecision> AcquireAsync(string clientKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
