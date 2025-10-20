using System.Collections.Generic;

namespace LORE_LLM.Domain.Clusters;

public sealed record ClusterContext(
    string ClusterId,
    IReadOnlyList<string> MemberIds,
    IReadOnlyList<string>? SharedContext = null,
    IReadOnlyList<string>? KnowledgeReferences = null,
    double? Confidence = null,
    string? Notes = null);
