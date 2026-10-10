namespace RateLimiting.Server.Features.RateLimitingRules;

public sealed class RateLimitingRulesOptions
{
    public const string SectionName = "RateLimiting:Rules";

    public string FilePath { get; set; } = "rate-limit-rules.json";
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(5);
    public RejectedRequestBehavior RejectedRequestBehavior { get; set; } = RejectedRequestBehavior.Drop;
    public string QueueKey { get; set; } = "rate-limit:rejected-requests";
    public int QueueMaxLength { get; set; } = 10_000;
    public int QueuedBodyMaxBytes { get; set; } = 64 * 1024;
}

public enum RateLimitUnit
{
    Second,
    Minute,
    Hour,
    Day
}

public enum RejectedRequestBehavior
{
    Drop,
    Queue
}
