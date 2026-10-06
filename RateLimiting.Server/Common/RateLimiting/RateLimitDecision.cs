namespace RateLimiting.Server.Common.RateLimiting;

public readonly record struct RateLimitDecision(bool IsAllowed, TimeSpan? RetryAfter = null)
{
    public static RateLimitDecision Allow() => new(true);

    public static RateLimitDecision Reject(TimeSpan? retryAfter = null) => new(false, retryAfter);
}
