namespace RateLimiting.Server.Features.FixedWindowCounter;

public sealed class FixedWindowCounterOptions
{
    public const string SectionName = "RateLimiting:FixedWindowCounter";

    public int Limit { get; set; } = 5;
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(10);
}
