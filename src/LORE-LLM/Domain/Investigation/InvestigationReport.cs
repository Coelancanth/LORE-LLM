using System;
using System.Collections.Generic;

namespace LORE_LLM.Domain.Investigation;

public sealed record InvestigationReport(
    string Project,
    string ProjectDisplayName,
    DateTimeOffset GeneratedAt,
    string InputHash,
    IReadOnlyList<InvestigationSuggestion> Suggestions);
