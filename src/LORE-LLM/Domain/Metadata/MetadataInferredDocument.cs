using System;
using System.Collections.Generic;

namespace LORE_LLM.Domain.Metadata;

public sealed record MetadataInferredDocument(
    string Project,
    string ProjectDisplayName,
    DateTimeOffset GeneratedAt,
    string SourceTextHash,
    IReadOnlyList<SegmentMetadata> Segments);
