using RateLimiting.Server.Common;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.FixedWindowCounter;

public static class FixedWindowCounterEndpoint
{
    private const string Algorithm = "fixed-window-counter";

    public static IServiceCollection AddFixedWindowCounter(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FixedWindowCounterOptions>(configuration.GetSection(FixedWindowCounterOptions.SectionName));
        services.AddSingleton<FixedWindowCounterLimiter>();
        return services;
    }

    public static IEndpointRouteBuilder MapFixedWindowCounter(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet($"/{Algorithm}", (TimeProvider timeProvider) =>
                TypedResults.Ok(new AlgorithmResponse(Algorithm, timeProvider.GetUtcNow())))
            .RequireRateLimiter<FixedWindowCounterLimiter>()
            .WithName("FixedWindowCounter");

        return endpoints;
    }
}
