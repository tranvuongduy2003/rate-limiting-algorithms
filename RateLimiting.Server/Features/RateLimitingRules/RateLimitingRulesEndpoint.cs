namespace RateLimiting.Server.Features.RateLimitingRules;

public static class RateLimitingRulesEndpoint
{
    public static IServiceCollection AddRateLimitingRules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RateLimitingRulesOptions>(
            configuration.GetSection(RateLimitingRulesOptions.SectionName));
        services.AddSingleton<FileRateLimitingRuleStore>();
        services.AddSingleton<RateLimitingRuleCache>();
        services.AddSingleton<ConfiguredRateLimiter>();
        services.AddSingleton<RejectedRequestQueue>();
        services.AddHostedService<RateLimitingRuleRefreshWorker>();
        return services;
    }

    public static IApplicationBuilder UseRateLimitingRules(this IApplicationBuilder app) =>
        app.UseMiddleware<RateLimitingRuleMiddleware>();

    public static IEndpointRouteBuilder MapRateLimitingRules(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/rate-limiting-rules", (ConfiguredRateLimiter limiter) =>
                TypedResults.Ok(limiter.Rules))
            .WithName("GetRateLimitingRules");

        endpoints.MapPost("/rate-limiting-rules/evaluate", Evaluate)
            .RequireConfiguredRateLimitFromBody()
            .WithName("EvaluateRateLimitingRule");

        endpoints.MapGet("/rate-limiting-rules/queue", async (
                RejectedRequestQueue queue,
                CancellationToken cancellationToken) =>
            {
                var length = await queue.GetLengthAsync(cancellationToken);
                return TypedResults.Ok(queue.CreateStatus(length));
            })
            .WithName("GetRejectedRequestQueueStatus");

        // These handlers stand in for downstream API servers. The middleware runs first,
        // matches their metadata to the cached disk rule, and only forwards allowed calls.
        endpoints.MapPost("/messages/marketing", (TimeProvider timeProvider) =>
                TypedResults.Ok(new RateLimitingRuleResponse(
                    "messaging",
                    "message_type",
                    "marketing",
                    timeProvider.GetUtcNow())))
            .RequireConfiguredRateLimit("messaging", "message_type", "marketing")
            .WithName("SendMarketingMessage");

        endpoints.MapPost("/auth/login", (TimeProvider timeProvider) =>
                TypedResults.Ok(new RateLimitingRuleResponse(
                    "auth",
                    "auth_type",
                    "login",
                    timeProvider.GetUtcNow())))
            .RequireConfiguredRateLimit("auth", "auth_type", "login")
            .WithName("AttemptLogin");

        return endpoints;
    }

    private static IResult Evaluate(
        RateLimitingRuleRequest request,
        TimeProvider timeProvider)
    {
        return Results.Ok(new RateLimitingRuleResponse(
            request.Domain,
            request.DescriptorKey,
            request.DescriptorValue,
            timeProvider.GetUtcNow()));
    }
}
