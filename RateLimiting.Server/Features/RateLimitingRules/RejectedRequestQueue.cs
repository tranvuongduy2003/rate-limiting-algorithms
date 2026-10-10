using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace RateLimiting.Server.Features.RateLimitingRules;

public sealed class RejectedRequestQueue(
    IConnectionMultiplexer connection,
    IOptions<RateLimitingRulesOptions> options,
    TimeProvider timeProvider,
    ILogger<RejectedRequestQueue> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabase _database = connection.GetDatabase();
    private readonly RateLimitingRulesOptions _options = options.Value;

    public async Task<string> HandleAsync(
        HttpContext context,
        string clientKey,
        RuleEvaluation evaluation,
        CancellationToken cancellationToken)
    {
        if (_options.RejectedRequestBehavior == RejectedRequestBehavior.Drop)
        {
            return "dropped";
        }

        try
        {
            var (body, truncated) = await ReadBodyAsync(context.Request, cancellationToken);
            var request = context.Request;
            var message = new RateLimitedRequestMessage(
                clientKey,
                evaluation.Rule,
                request.Method,
                $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}{request.QueryString}",
                request.ContentType,
                body,
                truncated,
                timeProvider.GetUtcNow(),
                evaluation.Decision.RetryAfter?.TotalSeconds);

            var payload = JsonSerializer.Serialize(message, SerializerOptions);
            await _database.StreamAddAsync(
                _options.QueueKey,
                [new NameValueEntry("payload", payload)]);
            await _database.StreamTrimAsync(_options.QueueKey, _options.QueueMaxLength, useApproximateMaxLength: true);
            return "queued";
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Could not enqueue a rejected request in Redis stream {QueueKey}.", _options.QueueKey);
            return "queue-unavailable";
        }
    }

    public async Task<long> GetLengthAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_options.RejectedRequestBehavior == RejectedRequestBehavior.Drop)
        {
            return 0;
        }

        try
        {
            return await _database.StreamLengthAsync(_options.QueueKey);
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Could not read Redis stream {QueueKey}.", _options.QueueKey);
            return -1;
        }
    }

    public RejectedRequestQueueStatus CreateStatus(long length) =>
        new(_options.RejectedRequestBehavior.ToString().ToLowerInvariant(), _options.QueueKey, length);

    private async Task<(string? Body, bool Truncated)> ReadBodyAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is 0 or null && !request.Headers.ContainsKey("Transfer-Encoding"))
        {
            return (null, false);
        }

        request.EnableBuffering();
        var bytes = new byte[_options.QueuedBodyMaxBytes + 1];
        var read = 0;
        while (read < bytes.Length)
        {
            var count = await request.Body.ReadAsync(bytes.AsMemory(read, bytes.Length - read), cancellationToken);
            if (count == 0)
            {
                break;
            }

            read += count;
        }

        request.Body.Position = 0;
        var truncated = read > _options.QueuedBodyMaxBytes;
        var bodyLength = Math.Min(read, _options.QueuedBodyMaxBytes);
        return (Encoding.UTF8.GetString(bytes, 0, bodyLength), truncated);
    }

}
