using CSharpFunctionalExtensions;
using LORE_LLM.Domain.Extraction;

namespace LORE_LLM.Application.Enrichment.Enrichers;

public sealed class IdPrefixEnricher : IMetadataEnricher
{
    public bool CanApply(MetadataEnrichmentConfig config) => config.IdPrefixRules is not null && config.IdPrefixRules.Count > 0;

    public Task<Result<SourceTextRawDocument>> ApplyAsync(DirectoryInfo projectDirectory, SourceTextRawDocument document, MetadataEnrichmentConfig config, CancellationToken cancellationToken)
    {
        if (config.IdPrefixRules is null)
        {
            return Task.FromResult(Result.Success(document));
        }

        var updatedSegments = new List<SourceSegment>(document.Segments.Count);
        foreach (var segment in document.Segments)
        {
            var merged = segment;
            foreach (var (prefix, metadata) in config.IdPrefixRules)
            {
                if (segment.Id.StartsWith(prefix, StringComparison.Ordinal))
                {
                    merged = MergeMetadata(merged, metadata);
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


