using RateLimiting.Server.Common;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.LeakingBucket;

public static class LeakingBucketEndpoint
{
    private const string Algorithm = "leaking-bucket";

    public static IServiceCollection AddLeakingBucket(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LeakingBucketOptions>(configuration.GetSection(LeakingBucketOptions.SectionName));
        services.AddSingleton<LeakingBucketLimiter>();
        return services;
    }

    public static IEndpointRouteBuilder MapLeakingBucket(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet($"/{Algorithm}", (TimeProvider timeProvider) =>
                TypedResults.Ok(new AlgorithmResponse(Algorithm, timeProvider.GetUtcNow())))
            .RequireRateLimiter<LeakingBucketLimiter>()
            .WithName("LeakingBucket");

        return endpoints;
    }
}
