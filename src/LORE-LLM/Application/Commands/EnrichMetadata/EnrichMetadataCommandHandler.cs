using System.Text.Json;
using System.Collections.Generic;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.Enrichment;
using LORE_LLM.Application.PostProcessing;

namespace LORE_LLM.Application.Commands.EnrichMetadata;

public sealed class EnrichMetadataCommandHandler : ICommandHandler<EnrichMetadataCommandOptions>
{
    private readonly MetadataEnrichmentPipeline _pipeline;
    private readonly IProjectNameSanitizer _sanitizer;

    public EnrichMetadataCommandHandler(MetadataEnrichmentPipeline pipeline, IProjectNameSanitizer sanitizer)
    {
        _pipeline = pipeline;
        _sanitizer = sanitizer;
    }

    public async Task<Result<int>> HandleAsync(EnrichMetadataCommandOptions options, CancellationToken cancellationToken)
    {
        var sanitized = _sanitizer.Sanitize(options.Project);
        var projectDir = new DirectoryInfo(Path.Combine(options.Workspace.FullName, sanitized));
        if (!projectDir.Exists)
        {
            return Result.Failure<int>($"Project folder not found: {projectDir.FullName}");
        }

        var config = await LoadConfigAsync(options.Workspace, sanitized, options.Config, cancellationToken);
        var result = await _pipeline.RunAsync(projectDir, config, cancellationToken);
        return result.IsSuccess ? Result.Success(0) : Result.Failure<int>(result.Error);
    }

    private static async Task<MetadataEnrichmentConfig> LoadConfigAsync(DirectoryInfo workspace, string sanitizedProject, FileInfo? specificConfig, CancellationToken cancellationToken)
    {
        // Precedence: repo default -> project override -> workspace override -> specific file
        var configs = new List<string>
        {
            Path.Combine("config", "metadata.enrichment.json"),
            Path.Combine("config", sanitizedProject, "metadata.enrichment.json"),
            Path.Combine(workspace.FullName, sanitizedProject, "metadata.enrichment.json")
        };
        if (specificConfig is not null)
        {
            configs.Add(specificConfig.FullName);
        }

        var merged = new MetadataEnrichmentConfig(
            IdPrefixRules: new Dictionary<string, IReadOnlyDictionary<string, string>>(),
            IdLookupRules: new Dictionary<string, IReadOnlyDictionary<string, string>>(),
            PathPatternRules: new List<PathPatternRule>());

        foreach (var path in configs)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            using var stream = File.OpenRead(path);
            var next = await JsonSerializer.DeserializeAsync<MetadataEnrichmentConfig>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
            if (next is null)
            {
                continue;
            }

            merged = Merge(merged, next);
        }

        return merged;
    }

    private static MetadataEnrichmentConfig Merge(MetadataEnrichmentConfig a, MetadataEnrichmentConfig b)
    {
        var idPrefix = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        if (a.IdPrefixRules is not null)
        {
            foreach (var kv in a.IdPrefixRules) idPrefix[kv.Key] = kv.Value;
        }
        if (b.IdPrefixRules is not null)
        {
            foreach (var kv in b.IdPrefixRules) idPrefix[kv.Key] = kv.Value;
        }

        var idLookup = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        if (a.IdLookupRules is not null)
        {
            foreach (var kv in a.IdLookupRules) idLookup[kv.Key] = kv.Value;
        }
        if (b.IdLookupRules is not null)
        {
            foreach (var kv in b.IdLookupRules) idLookup[kv.Key] = kv.Value;
        }

        var pathRules = new List<PathPatternRule>();
        if (a.PathPatternRules is not null) pathRules.AddRange(a.PathPatternRules);
        if (b.PathPatternRules is not null) pathRules.AddRange(b.PathPatternRules);

        return new MetadataEnrichmentConfig(idPrefix, idLookup, pathRules);
    }
}


