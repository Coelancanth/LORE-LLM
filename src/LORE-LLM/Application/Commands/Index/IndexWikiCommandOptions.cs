using System.IO;

namespace LORE_LLM.Application.Commands.Index;

public sealed record IndexWikiCommandOptions(
    DirectoryInfo Workspace,
    string Project,
    bool ForceRefresh,
    bool WithVector = false,
    string QdrantEndpoint = "http://localhost:6333",
    string? QdrantApiKey = null,
    string QdrantCollection = "lore_llm_wiki",
    int VectorDimension = 384,
    string EmbeddingSource = "none");


