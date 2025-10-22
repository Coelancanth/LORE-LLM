using CSharpFunctionalExtensions;
using LORE_LLM.Domain.Extraction;

namespace LORE_LLM.Application.Enrichment.Enrichers;

public sealed class IdLookupEnricher : IMetadataEnricher
{
    public bool CanApply(MetadataEnrichmentConfig config) => config.IdLookupRules is not null && config.IdLookupRules.Count > 0;

    public Task<Result<SourceTextRawDocument>> ApplyAsync(DirectoryInfo projectDirectory, SourceTextRawDocument document, MetadataEnrichmentConfig config, CancellationToken cancellationToken)
    {
        if (config.IdLookupRules is null)
        {
            return Task.FromResult(Result.Success(document));
        }

        var updatedSegments = new List<SourceSegment>(document.Segments.Count);
        foreach (var segment in document.Segments)
        {
            if (config.IdLookupRules.TryGetValue(segment.Id, out var metadata))
            {
                updatedSegments.Add(MergeMetadata(segment, metadata));
            }
            else
            {
                updatedSegments.Add(segment);
            }
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


