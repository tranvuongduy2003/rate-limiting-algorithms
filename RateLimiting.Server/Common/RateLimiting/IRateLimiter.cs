namespace RateLimiting.Server.Common.RateLimiting;

public interface IRateLimiter
{
    ValueTask<RateLimitDecision> AcquireAsync(string clientKey, CancellationToken cancellationToken = default);
}
