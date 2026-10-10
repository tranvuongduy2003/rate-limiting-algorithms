using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RateLimiting.Server.Common.RateLimiting;

namespace RateLimiting.Server.Features.RateLimitingRules;

public sealed class ConfiguredRateLimiter
{
    private readonly TimeProvider _timeProvider;
    private readonly IReadOnlyList<ConfiguredRule> _rules;
    private readonly Dictionary<string, ConfiguredRule> _rulesByKey;
    private readonly ConcurrentDictionary<string, WindowCounter> _counters = new();

    public ConfiguredRateLimiter(IOptions<RateLimitingRulesOptions> options, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _rules = options.Value.Policies.Select(Validate).ToArray();
        _rulesByKey = _rules.ToDictionary(
            static rule => CreateRuleKey(rule.Definition.Domain, rule.Definition.DescriptorKey, rule.Definition.DescriptorValue),
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<RateLimitingRuleDefinition> Rules => _rules.Select(static rule => rule.Definition).ToArray();

    public RuleEvaluation? Acquire(RateLimitingRuleRequest request, string clientKey)
    {
        if (!_rulesByKey.TryGetValue(
                CreateRuleKey(request.Domain, request.DescriptorKey, request.DescriptorValue),
                out var rule))
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        var windowNumber = now.UtcTicks / rule.Window.Ticks;
        var counterKey = $"{rule.Key}:{clientKey}";
        var counter = _counters.GetOrAdd(counterKey, static (_, number) => new WindowCounter(number), windowNumber);

        lock (counter)
        {
            if (counter.WindowNumber != windowNumber)
            {
                counter.WindowNumber = windowNumber;
                counter.Count = 0;
            }

            if (counter.Count < rule.Definition.RequestsPerUnit)
            {
                counter.Count++;
                return new RuleEvaluation(
                    rule.Definition,
                    RateLimitDecision.Allow(
                        rule.Definition.RequestsPerUnit,
                        rule.Definition.RequestsPerUnit - counter.Count));
            }

            var elapsedWindowTicks = now.UtcTicks % rule.Window.Ticks;
            var retryAfter = TimeSpan.FromTicks(rule.Window.Ticks - elapsedWindowTicks);
            return new RuleEvaluation(
                rule.Definition,
                RateLimitDecision.Reject(rule.Definition.RequestsPerUnit, retryAfter));
        }
    }

    private static ConfiguredRule Validate(RateLimitingRuleOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DescriptorKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DescriptorValue);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.RequestsPerUnit, 1);

        var definition = new RateLimitingRuleDefinition(
            options.Domain,
            options.DescriptorKey,
            options.DescriptorValue,
            options.Unit.ToString().ToLowerInvariant(),
            options.RequestsPerUnit);

        return new ConfiguredRule(
            CreateRuleKey(options.Domain, options.DescriptorKey, options.DescriptorValue),
            definition,
            options.Unit switch
            {
                RateLimitUnit.Second => TimeSpan.FromSeconds(1),
                RateLimitUnit.Minute => TimeSpan.FromMinutes(1),
                RateLimitUnit.Hour => TimeSpan.FromHours(1),
                RateLimitUnit.Day => TimeSpan.FromDays(1),
                _ => throw new ArgumentOutOfRangeException(nameof(options.Unit), options.Unit, "Unsupported rate limit unit.")
            });
    }

    private static string CreateRuleKey(string domain, string descriptorKey, string descriptorValue) =>
        $"{domain.Trim()}\u001f{descriptorKey.Trim()}\u001f{descriptorValue.Trim()}";

    private sealed record ConfiguredRule(string Key, RateLimitingRuleDefinition Definition, TimeSpan Window);

    private sealed class WindowCounter(long windowNumber)
    {
        public long WindowNumber { get; set; } = windowNumber;
        public int Count { get; set; }
    }
}

public sealed record RuleEvaluation(RateLimitingRuleDefinition Rule, RateLimitDecision Decision);
