using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.RateLimitingRules;

public sealed class ConfiguredRateLimiter(
    RateLimitingRuleCache cache,
    RedisRateLimitStore store)
{
    private const string AcquireScript = """
        local now_parts = redis.call('TIME')
        local now_ms = (tonumber(now_parts[1]) * 1000) + math.floor(tonumber(now_parts[2]) / 1000)
        local window_ms = tonumber(ARGV[1])
        local request_limit = tonumber(ARGV[2])
        local window_number = math.floor(now_ms / window_ms)
        local state = redis.call('HMGET', KEYS[1], 'window', 'count')
        local stored_window = tonumber(state[1])
        local count = tonumber(state[2]) or 0

        if stored_window ~= window_number then
            count = 0
        end

        local allowed = 0
        if count < request_limit then
            count = count + 1
            allowed = 1
        end

        local retry_after_ms = window_ms - (now_ms % window_ms)
        redis.call('HSET', KEYS[1],
            'window', window_number,
            'count', count,
            'last_request_ms', now_ms)
        redis.call('PEXPIRE', KEYS[1], retry_after_ms)
        return { allowed, retry_after_ms, math.max(0, request_limit - count) }
        """;

    public IReadOnlyList<RateLimitingRuleDefinition> Rules => cache.Rules;

    public async ValueTask<RuleEvaluation> AcquireAsync(
        CachedRateLimitingRule rule,
        string clientKey,
        CancellationToken cancellationToken = default)
    {
        var result = await store.EvaluateAsync(
            AcquireScript,
            RedisRateLimitStore.CreateKey($"rule:{rule.Key}", clientKey),
            [(long)Math.Ceiling(rule.Window.TotalMilliseconds), rule.Definition.RequestsPerUnit],
            cancellationToken);

        if (result is null)
        {
            return new RuleEvaluation(
                rule.Definition,
                RateLimitDecision.Allow(rule.Definition.RequestsPerUnit, rule.Definition.RequestsPerUnit));
        }

        var decision = (long)result[0] == 1
            ? RateLimitDecision.Allow(rule.Definition.RequestsPerUnit, (int)(long)result[2])
            : RateLimitDecision.Reject(
                rule.Definition.RequestsPerUnit,
                TimeSpan.FromMilliseconds((long)result[1]));

        return new RuleEvaluation(rule.Definition, decision);
    }
}

public sealed record RuleEvaluation(RateLimitingRuleDefinition Rule, RateLimitDecision Decision);
