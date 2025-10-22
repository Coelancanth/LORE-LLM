namespace LORE_LLM.Application.Retrieval;

public interface IDeterministicEmbeddingProvider
{
	float[] Embed(string text, int dimension);
}


