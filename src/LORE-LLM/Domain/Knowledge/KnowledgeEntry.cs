using System.Collections.Generic;

namespace LORE_LLM.Domain.Knowledge;

public sealed record KnowledgeEntry(
    string ConceptId,
    string Title,
    string Summary,
    KnowledgeSource Source,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyList<string>? Categories = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    IReadOnlyList<string>? GlossaryHints = null);
