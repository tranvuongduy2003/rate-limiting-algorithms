using RateLimiting.Server.Common;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.SlidingWindowCounter;

public static class SlidingWindowCounterEndpoint
{
    private const string Algorithm = "sliding-window-counter";
    private const string RedisAlgorithm = "redis-sliding-window-counter";

    public static IServiceCollection AddSlidingWindowCounter(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SlidingWindowCounterOptions>(configuration.GetSection(SlidingWindowCounterOptions.SectionName));
        services.AddSingleton<SlidingWindowCounterLimiter>();
        services.AddSingleton<RedisSlidingWindowCounterLimiter>();
        return services;
    }

    public static IEndpointRouteBuilder MapSlidingWindowCounter(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet($"/{Algorithm}", (TimeProvider timeProvider) =>
                TypedResults.Ok(new AlgorithmResponse(Algorithm, timeProvider.GetUtcNow())))
            .RequireRateLimiter<SlidingWindowCounterLimiter>()
            .WithName("SlidingWindowCounter");

        endpoints.MapGet($"/{RedisAlgorithm}", (TimeProvider timeProvider) =>
                TypedResults.Ok(new AlgorithmResponse(RedisAlgorithm, timeProvider.GetUtcNow())))
            .RequireRateLimiter<RedisSlidingWindowCounterLimiter>()
            .WithName("RedisSlidingWindowCounter");

        return endpoints;
    }
}
