using System.Globalization;

namespace RateLimiting.Server.Common.RateLimiting;

public static class RateLimitResponseHeaders
{
    public const string Limit = "X-RateLimit-Limit";
    public const string Remaining = "X-RateLimit-Remaining";
    public const string RetryAfter = "X-RateLimit-Retry-After";

    public static void Apply(HttpResponse response, RateLimitDecision decision)
    {
        response.Headers[Limit] = decision.Limit.ToString(CultureInfo.InvariantCulture);
        response.Headers[Remaining] = decision.Remaining.ToString(CultureInfo.InvariantCulture);

        if (decision.RetryAfter is not { } retryAfter)
        {
            return;
        }

        var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        var value = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        response.Headers[RetryAfter] = value;
        response.Headers.RetryAfter = value;
    }
}
