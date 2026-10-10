namespace RateLimiting.Server.Features.RateLimitingRules;

public sealed record RateLimitingRuleDefinition(
    string Domain,
    string DescriptorKey,
    string DescriptorValue,
    string Unit,
    int RequestsPerUnit);

public sealed record RateLimitingRuleRequest(
    string Domain,
    string DescriptorKey,
    string DescriptorValue);

public sealed record RateLimitingRuleResponse(
    string Domain,
    string DescriptorKey,
    string DescriptorValue,
    DateTimeOffset AcceptedAt);

public sealed class RateLimitingRuleDocument
{
    public List<RateLimitingRuleDefinition> Policies { get; set; } = [];
}

public sealed record RateLimitedRequestMessage(
    string ClientKey,
    RateLimitingRuleDefinition Rule,
    string Method,
    string Url,
    string? ContentType,
    string? Body,
    bool BodyTruncated,
    DateTimeOffset RejectedAt,
    double? RetryAfterSeconds);

public sealed record RejectedRequestQueueStatus(
    string Behavior,
    string QueueKey,
    long Length);
