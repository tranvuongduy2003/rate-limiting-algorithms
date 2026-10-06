namespace RateLimiting.Server.Features.SlidingWindowCounter;

public sealed class SlidingWindowCounterOptions
{
    public const string SectionName = "RateLimiting:SlidingWindowCounter";

    public int Limit { get; set; } = 5;
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(10);
}
