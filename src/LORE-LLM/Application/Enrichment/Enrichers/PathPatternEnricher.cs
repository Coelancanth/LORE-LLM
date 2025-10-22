using CSharpFunctionalExtensions;
using LORE_LLM.Domain.Extraction;

namespace LORE_LLM.Application.Enrichment.Enrichers;

public sealed class PathPatternEnricher : IMetadataEnricher
{
    public bool CanApply(MetadataEnrichmentConfig config) => config.PathPatternRules is not null && config.PathPatternRules.Count > 0;

    public Task<Result<SourceTextRawDocument>> ApplyAsync(DirectoryInfo projectDirectory, SourceTextRawDocument document, MetadataEnrichmentConfig config, CancellationToken cancellationToken)
    {
        if (config.PathPatternRules is null)
        {
            return Task.FromResult(Result.Success(document));
        }

        var updatedSegments = new List<SourceSegment>(document.Segments.Count);
        foreach (var segment in document.Segments)
        {
            var merged = segment;
            foreach (var rule in config.PathPatternRules)
            {
                if (rule.Contains is not null && document.SourcePath.Contains(rule.Contains, StringComparison.OrdinalIgnoreCase))
                {
                    merged = MergeMetadata(merged, rule.Metadata);
                }
                if (rule.StartsWith is not null && document.SourcePath.StartsWith(rule.StartsWith, StringComparison.OrdinalIgnoreCase))
                {
                    merged = MergeMetadata(merged, rule.Metadata);
                }
                if (rule.EndsWith is not null && document.SourcePath.EndsWith(rule.EndsWith, StringComparison.OrdinalIgnoreCase))
                {
                    merged = MergeMetadata(merged, rule.Metadata);
                }
            }
            updatedSegments.Add(merged);
        }

        var updatedDoc = document with { Segments = updatedSegments };
        return Task.FromResult(Result.Success(updatedDoc));
    }

    private static SourceSegment MergeMetadata(SourceSegment segment, IReadOnlyDictionary<string, string> metadata)
    {
        var current = segment.Metadata is null ? new Dictionary<string, string>() : new Dictionary<string, string>(segment.Metadata);
        foreach (var kv in metadata)
        {
            if (!current.ContainsKey(kv.Key))
            {
                current[kv.Key] = kv.Value;
            }
        }
        return segment with { Metadata = current };
    }
}


