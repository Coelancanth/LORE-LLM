using System.Collections.Generic;

namespace LORE_LLM.Domain.Metadata;

public sealed record SegmentMetadata(
    string SegmentId,
    string? Speaker = null,
    string? SpeakerRole = null,
    string? Tone = null,
    string? Scope = null,
    string? Summary = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<string>? GlossaryTerms = null,
    double? Confidence = null,
    IReadOnlyList<string>? KnowledgeReferences = null);
