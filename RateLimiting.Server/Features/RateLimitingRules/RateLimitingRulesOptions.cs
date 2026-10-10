namespace RateLimiting.Server.Features.RateLimitingRules;

public sealed class RateLimitingRulesOptions
{
    public const string SectionName = "RateLimiting:Rules";

    public List<RateLimitingRuleOptions> Policies { get; set; } = [];
}

public sealed class RateLimitingRuleOptions
{
    public string Domain { get; set; } = string.Empty;
    public string DescriptorKey { get; set; } = string.Empty;
    public string DescriptorValue { get; set; } = string.Empty;
    public RateLimitUnit Unit { get; set; }
    public int RequestsPerUnit { get; set; }
}

public enum RateLimitUnit
{
    Second,
    Minute,
    Hour,
    Day
}
