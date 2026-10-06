namespace RateLimiting.Server.Common;

public sealed record AlgorithmResponse(string Algorithm, DateTimeOffset ServedAt);
