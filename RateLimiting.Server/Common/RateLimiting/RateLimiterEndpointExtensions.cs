namespace RateLimiting.Server.Common.RateLimiting;

public static class RateLimiterEndpointExtensions
{
    public static RouteHandlerBuilder RequireRateLimiter<TLimiter>(this RouteHandlerBuilder builder)
        where TLimiter : IRateLimiter =>
        builder.AddEndpointFilter<RateLimiterEndpointFilter<TLimiter>>();
}
