using RateLimiting.Server.Common;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.SlidingWindowLog;

public static class SlidingWindowLogEndpoint
{
    private const string Algorithm = "sliding-window-log";
    private const string RedisAlgorithm = "redis-sliding-window-log";

    public static IServiceCollection AddSlidingWindowLog(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SlidingWindowLogOptions>(configuration.GetSection(SlidingWindowLogOptions.SectionName));
        services.AddSingleton<SlidingWindowLogLimiter>();
        services.AddSingleton<RedisSlidingWindowLogLimiter>();
        return services;
    }

    public static IEndpointRouteBuilder MapSlidingWindowLog(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet($"/{Algorithm}", (TimeProvider timeProvider) =>
                TypedResults.Ok(new AlgorithmResponse(Algorithm, timeProvider.GetUtcNow())))
            .RequireRateLimiter<SlidingWindowLogLimiter>()
            .WithName("SlidingWindowLog");

        endpoints.MapGet($"/{RedisAlgorithm}", (TimeProvider timeProvider) =>
                TypedResults.Ok(new AlgorithmResponse(RedisAlgorithm, timeProvider.GetUtcNow())))
            .RequireRateLimiter<RedisSlidingWindowLogLimiter>()
            .WithName("RedisSlidingWindowLog");

        return endpoints;
    }
}
