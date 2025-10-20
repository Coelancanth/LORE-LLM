namespace LORE_LLM.Domain.Extraction;

public sealed record SourceSegment(
    string Id,
    string Text,
    bool IsEmpty,
    int LineNumber);
