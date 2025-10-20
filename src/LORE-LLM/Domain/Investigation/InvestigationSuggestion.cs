using System.Collections.Generic;

namespace LORE_LLM.Domain.Investigation;

public sealed record InvestigationSuggestion(
    string SegmentId,
    IReadOnlyList<string> Tokens,
    IReadOnlyList<InvestigationCandidate> Candidates);
