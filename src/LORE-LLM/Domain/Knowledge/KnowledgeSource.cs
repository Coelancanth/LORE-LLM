using System;

namespace LORE_LLM.Domain.Knowledge;

public sealed record KnowledgeSource(
    string Provider,
    string Url,
    string License,
    DateTimeOffset RetrievedAt);
