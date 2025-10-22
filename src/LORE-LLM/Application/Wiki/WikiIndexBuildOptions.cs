using System.IO;

namespace LORE_LLM.Application.Wiki;

public sealed record WikiIndexBuildOptions(
	DirectoryInfo Workspace,
	string ProjectDisplayName,
	bool ForceRefresh,
	bool WithVector,
	string QdrantEndpoint,
	string? QdrantApiKey,
	string QdrantCollection,
	int VectorDimension,
	string EmbeddingSource);


