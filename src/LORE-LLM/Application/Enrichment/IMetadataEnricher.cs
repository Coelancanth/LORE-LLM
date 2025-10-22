using CSharpFunctionalExtensions;
using LORE_LLM.Domain.Extraction;

namespace LORE_LLM.Application.Enrichment;

public interface IMetadataEnricher
{
    bool CanApply(MetadataEnrichmentConfig config);
    Task<Result<SourceTextRawDocument>> ApplyAsync(DirectoryInfo projectDirectory, SourceTextRawDocument document, MetadataEnrichmentConfig config, CancellationToken cancellationToken);
}


