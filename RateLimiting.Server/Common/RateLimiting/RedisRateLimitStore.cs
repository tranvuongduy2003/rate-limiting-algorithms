using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;

namespace RateLimiting.Server.Common.RateLimiting;

public sealed class RedisRateLimitStore(
    IConnectionMultiplexer connection,
    ILogger<RedisRateLimitStore> logger)
{
    private readonly IDatabase _database = connection.GetDatabase();

    public async ValueTask<RedisResult[]?> EvaluateAsync(
        string script,
        RedisKey key,
        RedisValue[] values,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var result = await _database.ScriptEvaluateAsync(script, [key], values);
            return (RedisResult[]?)result;
        }
        catch (RedisException exception)
        {
            // Rate limiting is an availability guard, not a reason to take the API down.
            // If Redis is unavailable, let the request through and surface the failure in telemetry.
            logger.LogWarning(exception, "Redis rate limiting is unavailable; allowing the request.");
            return null;
        }
    }

    public static RedisKey CreateKey(string algorithm, string clientKey)
    {
        var clientHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(clientKey)));
        return $"rate-limit:{algorithm}:{clientHash}";
    }
}
