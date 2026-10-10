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
