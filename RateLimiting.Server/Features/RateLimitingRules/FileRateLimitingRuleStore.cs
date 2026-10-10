using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RateLimiting.Server.Features.RateLimitingRules;

public sealed class FileRateLimitingRuleStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly string _filePath;

    public FileRateLimitingRuleStore(IOptions<RateLimitingRulesOptions> options, IHostEnvironment environment)
    {
        _filePath = Path.GetFullPath(options.Value.FilePath, environment.ContentRootPath);
    }

    public string FilePath => _filePath;

    public async Task<IReadOnlyList<RateLimitingRuleDefinition>> LoadAsync(CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            _filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var document = await JsonSerializer.DeserializeAsync<RateLimitingRuleDocument>(
            stream,
            SerializerOptions,
            cancellationToken);

        return document?.Policies ?? [];
    }
}
