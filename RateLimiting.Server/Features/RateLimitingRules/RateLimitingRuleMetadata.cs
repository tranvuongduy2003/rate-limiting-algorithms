namespace RateLimiting.Server.Features.RateLimitingRules;

public sealed record RateLimitingRuleMetadata(
    string? Domain,
    string? DescriptorKey,
    string? DescriptorValue,
    bool ReadDescriptorFromBody = false);

public static class RateLimitingRuleEndpointExtensions
{
    public static RouteHandlerBuilder RequireConfiguredRateLimit(
        this RouteHandlerBuilder builder,
        string domain,
        string descriptorKey,
        string descriptorValue) =>
        builder.WithMetadata(new RateLimitingRuleMetadata(domain, descriptorKey, descriptorValue));

    public static RouteHandlerBuilder RequireConfiguredRateLimitFromBody(this RouteHandlerBuilder builder) =>
        builder.WithMetadata(new RateLimitingRuleMetadata(null, null, null, ReadDescriptorFromBody: true));
}
