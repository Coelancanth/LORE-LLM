using System.Text.Json;
using CSharpFunctionalExtensions;
using LORE_LLM.Domain.Extraction;

namespace LORE_LLM.Application.Enrichment;

public sealed class MetadataEnrichmentPipeline
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IReadOnlyCollection<IMetadataEnricher> _enrichers;

    public MetadataEnrichmentPipeline(IEnumerable<IMetadataEnricher> enrichers)
    {
        _enrichers = enrichers.ToList();
    }

    public async Task<Result> RunAsync(DirectoryInfo projectDirectory, MetadataEnrichmentConfig config, CancellationToken cancellationToken)
    {
        var sourcePath = Path.Combine(projectDirectory.FullName, "source_text_raw.json");
        if (!File.Exists(sourcePath))
        {
            return Result.Failure($"Missing source_text_raw.json at {sourcePath}");
        }

        SourceTextRawDocument document;
        await using (var read = File.OpenRead(sourcePath))
        {
            var parsed = await JsonSerializer.DeserializeAsync<SourceTextRawDocument>(read, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
            if (parsed is null)
            {
                return Result.Failure("Unable to parse source_text_raw.json");
            }
            document = parsed;
        }

        foreach (var enricher in _enrichers)
        {
            if (!enricher.CanApply(config))
            {
                continue;
            }

            var result = await enricher.ApplyAsync(projectDirectory, document, config, cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }
            document = result.Value;
        }

        await using (var write = File.Create(sourcePath))
        {
            await JsonSerializer.SerializeAsync(write, document, JsonOptions, cancellationToken);
        }

        return Result.Success();
    }
}


