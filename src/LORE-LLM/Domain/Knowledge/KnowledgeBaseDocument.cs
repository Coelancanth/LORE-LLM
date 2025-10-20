using System;
using System.Collections.Generic;

namespace LORE_LLM.Domain.Knowledge;

public sealed record KnowledgeBaseDocument(
    string Project,
    string ProjectDisplayName,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<KnowledgeEntry> Entries);
