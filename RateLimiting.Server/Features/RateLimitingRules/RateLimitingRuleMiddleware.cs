using System.Text.Json;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.RateLimitingRules;

public sealed class RateLimitingRuleMiddleware(
    RequestDelegate next,
    ILogger<RateLimitingRuleMiddleware> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(
        HttpContext context,
        RateLimitingRuleCache cache,
        ConfiguredRateLimiter limiter,
        RejectedRequestQueue rejectedRequestQueue)
    {
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<RateLimitingRuleMetadata>();
        if (metadata is null)
        {
            await next(context);
            return;
        }

        var descriptor = await ResolveDescriptorAsync(context.Request, metadata, context.RequestAborted);
        if (descriptor is null)
        {
            // Let endpoint model binding produce the normal 400 response for a missing or malformed body.
            await next(context);
            return;
        }

        var rule = cache.Find(descriptor.Domain, descriptor.DescriptorKey, descriptor.DescriptorValue);
        if (rule is null)
        {
            if (metadata.ReadDescriptorFromBody)
            {
                await Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Rate limiting rule not found",
                        detail: "No configured rule matches the supplied domain and descriptor.")
                    .ExecuteAsync(context);
                return;
            }

            // A stale or temporarily unreadable rule file should not take the API down.
            logger.LogWarning(
                "No cached rate limiting rule matches {Domain}/{DescriptorKey}/{DescriptorValue}; allowing the request.",
                descriptor.Domain,
                descriptor.DescriptorKey,
                descriptor.DescriptorValue);
            await next(context);
            return;
        }

        var clientKey = RateLimitClientKey.Resolve(context);
        var evaluation = await limiter.AcquireAsync(rule, clientKey, context.RequestAborted);
        RateLimitResponseHeaders.Apply(context.Response, evaluation.Decision);

        if (evaluation.Decision.IsAllowed)
        {
            await next(context);
            return;
        }

        var handling = await rejectedRequestQueue.HandleAsync(
            context,
            clientKey,
            evaluation,
            context.RequestAborted);
        context.Response.Headers["X-RateLimit-Rejection-Handling"] = handling;

        await Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too many requests",
                detail: "The configured rate limit has been exceeded.")
            .ExecuteAsync(context);
    }

    private static async Task<RateLimitingRuleRequest?> ResolveDescriptorAsync(
        HttpRequest request,
        RateLimitingRuleMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (!metadata.ReadDescriptorFromBody)
        {
            return new RateLimitingRuleRequest(
                metadata.Domain!,
                metadata.DescriptorKey!,
                metadata.DescriptorValue!);
        }

        request.EnableBuffering();
        try
        {
            return await JsonSerializer.DeserializeAsync<RateLimitingRuleRequest>(
                request.Body,
                SerializerOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }
}
