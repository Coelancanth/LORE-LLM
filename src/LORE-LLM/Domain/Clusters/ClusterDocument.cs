using System;
using System.Collections.Generic;

namespace LORE_LLM.Domain.Clusters;

public sealed record ClusterDocument(
    string Project,
    string ProjectDisplayName,
    DateTimeOffset GeneratedAt,
    string SourceTextHash,
    IReadOnlyList<ClusterContext> Clusters);
