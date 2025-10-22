using CSharpFunctionalExtensions;

namespace LORE_LLM.Application.Retrieval;

public sealed class VectorRetrievalOrchestrator
{
	private readonly QdrantClient _qdrant;
	private readonly IDeterministicEmbeddingProvider _embedding;

	public VectorRetrievalOrchestrator(QdrantClient qdrant, IDeterministicEmbeddingProvider embedding)
	{
		_qdrant = qdrant;
		_embedding = embedding;
	}

	public Task<Result<IReadOnlyList<QdrantSearchHit>>> SearchAsync(
		string collection,
		string queryText,
		int dimension,
		int topK,
		IEnumerable<string>? keywordFilters,
		CancellationToken cancellationToken)
	{
		var vector = _embedding.Embed(queryText ?? string.Empty, dimension);
		return _qdrant.SearchAsync(collection, vector, topK, keywordFilters, cancellationToken);
	}
}


