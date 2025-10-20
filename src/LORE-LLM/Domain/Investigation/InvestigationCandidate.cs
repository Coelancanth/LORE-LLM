namespace LORE_LLM.Domain.Investigation;

public sealed record InvestigationCandidate(
    string ConceptId,
    string Source,
    double Confidence,
    string? Notes = null);
