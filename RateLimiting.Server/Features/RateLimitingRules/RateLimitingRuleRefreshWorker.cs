using Microsoft.Extensions.Options;

namespace RateLimiting.Server.Features.RateLimitingRules;

public sealed class RateLimitingRuleRefreshWorker(
    FileRateLimitingRuleStore store,
    RateLimitingRuleCache cache,
    IOptions<RateLimitingRulesOptions> options,
    TimeProvider timeProvider,
    ILogger<RateLimitingRuleRefreshWorker> logger) : BackgroundService
{
    private readonly TimeSpan _refreshInterval = options.Value.RefreshInterval;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Warm the cache before Kestrel starts accepting traffic.
        await RefreshAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_refreshInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not refresh rate limiting rules from {RuleFile}; keeping the last valid snapshot.", store.FilePath);
            }
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var rules = await store.LoadAsync(cancellationToken);
        cache.Replace(rules);
        logger.LogDebug("Loaded {RuleCount} rate limiting rules from {RuleFile}.", rules.Count, store.FilePath);
    }
}
