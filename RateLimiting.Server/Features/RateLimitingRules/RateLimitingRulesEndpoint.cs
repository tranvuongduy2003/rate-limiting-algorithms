using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.RateLimitingRules;

public static class RateLimitingRulesEndpoint
{
    public static IServiceCollection AddRateLimitingRules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RateLimitingRulesOptions>(configuration.GetSection(RateLimitingRulesOptions.SectionName));
        services.AddSingleton<ConfiguredRateLimiter>();
        return services;
    }

    public static IEndpointRouteBuilder MapRateLimitingRules(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/rate-limiting-rules", (ConfiguredRateLimiter limiter) =>
                TypedResults.Ok(limiter.Rules))
            .WithName("GetRateLimitingRules");

        endpoints.MapPost("/rate-limiting-rules/evaluate", Evaluate)
            .WithName("EvaluateRateLimitingRule");

        return endpoints;
    }

    private static IResult Evaluate(
        RateLimitingRuleRequest request,
        HttpContext httpContext,
        ConfiguredRateLimiter limiter,
        TimeProvider timeProvider)
    {
        var evaluation = limiter.Acquire(request, RateLimitClientKey.Resolve(httpContext));
        if (evaluation is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Rate limiting rule not found",
                detail: "No configured rule matches the supplied domain and descriptor.");
        }

        RateLimitResponseHeaders.Apply(httpContext.Response, evaluation.Decision);

        if (!evaluation.Decision.IsAllowed)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too many requests",
                detail: "The configured rate limit has been exceeded.");
        }

        return Results.Ok(new RateLimitingRuleResponse(
            evaluation.Rule.Domain,
            evaluation.Rule.DescriptorKey,
            evaluation.Rule.DescriptorValue,
            timeProvider.GetUtcNow()));
    }
}
