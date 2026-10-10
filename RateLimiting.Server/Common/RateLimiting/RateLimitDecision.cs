namespace RateLimiting.Server.Common.RateLimiting;

public readonly record struct RateLimitDecision(
    bool IsAllowed,
    int Limit,
    int Remaining,
    TimeSpan? RetryAfter = null)
{
    public static RateLimitDecision Allow(int limit, int remaining) =>
        new(true, limit, Math.Max(0, remaining));

    public static RateLimitDecision Reject(int limit, TimeSpan? retryAfter = null) =>
        new(false, limit, 0, retryAfter);
}
