namespace LORE_LLM.Domain.Extraction;

public sealed record SourceTextRawDocument(
    string SourcePath,
    DateTimeOffset GeneratedAt,
    string Project,
    string ProjectDisplayName,
    string InputHash,
    IReadOnlyList<SourceSegment> Segments);
