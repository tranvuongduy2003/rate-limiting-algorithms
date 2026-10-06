using System.Globalization;

namespace RateLimiting.Server.Common.RateLimiting;

public sealed class RateLimiterEndpointFilter<TLimiter>(TLimiter limiter) : IEndpointFilter
    where TLimiter : IRateLimiter
{
    private const string ClientIdHeader = "X-Client-Id";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var decision = await limiter.AcquireAsync(GetClientKey(httpContext), httpContext.RequestAborted);

        if (decision.IsAllowed)
        {
            return await next(context);
        }

        if (decision.RetryAfter is { } retryAfter)
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        return TypedResults.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Too many requests");
    }

    private static string GetClientKey(HttpContext httpContext)
    {
        var clientId = httpContext.Request.Headers[ClientIdHeader].ToString();
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            return clientId;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
    }
}
