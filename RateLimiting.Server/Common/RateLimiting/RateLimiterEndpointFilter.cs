namespace RateLimiting.Server.Common.RateLimiting;

public sealed class RateLimiterEndpointFilter<TLimiter>(TLimiter limiter) : IEndpointFilter
    where TLimiter : IRateLimiter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var decision = await limiter.AcquireAsync(RateLimitClientKey.Resolve(httpContext), httpContext.RequestAborted);
        RateLimitResponseHeaders.Apply(httpContext.Response, decision);

        if (decision.IsAllowed)
        {
            return await next(context);
        }

        return TypedResults.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Too many requests");
    }
}
