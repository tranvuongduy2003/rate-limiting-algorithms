namespace RateLimiting.Server.Features.RateLimitingRules;

public sealed class RateLimitingRuleCache
{
    private Snapshot _snapshot = Snapshot.Empty;

    public IReadOnlyList<RateLimitingRuleDefinition> Rules => Volatile.Read(ref _snapshot).Definitions;

    public CachedRateLimitingRule? Find(string domain, string descriptorKey, string descriptorValue)
    {
        var key = CreateRuleKey(domain, descriptorKey, descriptorValue);
        return Volatile.Read(ref _snapshot).ByKey.GetValueOrDefault(key);
    }

    public void Replace(IEnumerable<RateLimitingRuleDefinition> definitions)
    {
        var rules = definitions.Select(CreateCachedRule).ToArray();
        var byKey = rules.ToDictionary(static rule => rule.Key, StringComparer.OrdinalIgnoreCase);
        Volatile.Write(ref _snapshot, new Snapshot(rules.Select(static rule => rule.Definition).ToArray(), byKey));
    }

    public static string CreateRuleKey(string domain, string descriptorKey, string descriptorValue) =>
        $"{domain.Trim()}\u001f{descriptorKey.Trim()}\u001f{descriptorValue.Trim()}";

    private static CachedRateLimitingRule CreateCachedRule(RateLimitingRuleDefinition definition)
    {
        var unit = Enum.Parse<RateLimitUnit>(definition.Unit, ignoreCase: true);
        var normalized = definition with { Unit = unit.ToString().ToLowerInvariant() };
        var window = unit switch
        {
            RateLimitUnit.Second => TimeSpan.FromSeconds(1),
            RateLimitUnit.Minute => TimeSpan.FromMinutes(1),
            RateLimitUnit.Hour => TimeSpan.FromHours(1),
            RateLimitUnit.Day => TimeSpan.FromDays(1),
            _ => throw new ArgumentOutOfRangeException(nameof(definition), unit, "Unsupported rate limit unit.")
        };

        return new CachedRateLimitingRule(
            CreateRuleKey(normalized.Domain, normalized.DescriptorKey, normalized.DescriptorValue),
            normalized,
            window);
    }

    private sealed record Snapshot(
        IReadOnlyList<RateLimitingRuleDefinition> Definitions,
        IReadOnlyDictionary<string, CachedRateLimitingRule> ByKey)
    {
        public static Snapshot Empty { get; } = new(
            [],
            new Dictionary<string, CachedRateLimitingRule>(StringComparer.OrdinalIgnoreCase));
    }
}

public sealed record CachedRateLimitingRule(
    string Key,
    RateLimitingRuleDefinition Definition,
    TimeSpan Window);
