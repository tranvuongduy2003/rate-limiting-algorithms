namespace RateLimiting.Server.Features.TokenBucket;

public sealed class TokenBucketOptions
{
    public const string SectionName = "RateLimiting:TokenBucket";

    public int Capacity { get; set; } = 5;
    public double RefillRatePerSecond { get; set; } = 1;
}
