namespace RateLimiting.Server.Features.SlidingWindowLog;

public sealed class SlidingWindowLogOptions
{
    public const string SectionName = "RateLimiting:SlidingWindowLog";

    public int Limit { get; set; } = 5;
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(10);
}
