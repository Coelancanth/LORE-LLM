namespace LORE_LLM.Domain.Extraction;

public sealed record WorkspaceManifest(
    DateTimeOffset GeneratedAt,
    string InputPath,
    string InputHash,
    IReadOnlyDictionary<string, string> Artifacts);
