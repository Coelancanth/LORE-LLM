using System.Text.Json.Serialization;

namespace LORE_LLM.Application.Enrichment;

public sealed record MetadataEnrichmentConfig(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? IdPrefixRules = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? IdLookupRules = null,
    IReadOnlyList<PathPatternRule>? PathPatternRules = null);

public sealed record PathPatternRule(
    string? Contains,
    string? StartsWith,
    string? EndsWith,
    IReadOnlyDictionary<string, string> Metadata);


