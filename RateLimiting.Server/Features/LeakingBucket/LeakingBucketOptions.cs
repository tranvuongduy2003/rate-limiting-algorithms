namespace RateLimiting.Server.Features.LeakingBucket;

public sealed class LeakingBucketOptions
{
    public const string SectionName = "RateLimiting:LeakingBucket";

    public int Capacity { get; set; } = 5;
    public double LeakRatePerSecond { get; set; } = 1;
}
