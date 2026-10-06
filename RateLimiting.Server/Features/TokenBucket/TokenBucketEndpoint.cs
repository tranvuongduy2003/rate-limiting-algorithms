using RateLimiting.Server.Common;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.TokenBucket;

public static class TokenBucketEndpoint
{
    private const string Algorithm = "token-bucket";
    private const string RedisAlgorithm = "redis-token-bucket";

    public static IServiceCollection AddTokenBucket(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TokenBucketOptions>(configuration.GetSection(TokenBucketOptions.SectionName));
        services.AddSingleton<TokenBucketLimiter>();
        services.AddSingleton<RedisTokenBucketLimiter>();
        return services;
    }

    public static IEndpointRouteBuilder MapTokenBucket(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet($"/{Algorithm}", (TimeProvider timeProvider) =>
                TypedResults.Ok(new AlgorithmResponse(Algorithm, timeProvider.GetUtcNow())))
            .RequireRateLimiter<TokenBucketLimiter>()
            .WithName("TokenBucket");

        endpoints.MapGet($"/{RedisAlgorithm}", (TimeProvider timeProvider) =>
                TypedResults.Ok(new AlgorithmResponse(RedisAlgorithm, timeProvider.GetUtcNow())))
            .RequireRateLimiter<RedisTokenBucketLimiter>()
            .WithName("RedisTokenBucket");

        return endpoints;
    }
}
