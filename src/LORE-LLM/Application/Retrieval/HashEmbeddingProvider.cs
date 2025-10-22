using System.Security.Cryptography;
using System.Text;

namespace LORE_LLM.Application.Retrieval;

public sealed class HashEmbeddingProvider : IDeterministicEmbeddingProvider
{
	public float[] Embed(string text, int dimension)
	{
		// Deterministic, simple embedding: rolling SHA256 across token bytes into a fixed-size vector.
		// Not semantically meaningful but stable for testing/demo.
		if (dimension <= 0)
		{
			return Array.Empty<float>();
		}

		var vec = new float[dimension];
		var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
		Span<byte> hash = stackalloc byte[32];
		var offset = 0;
		while (offset < bytes.Length)
		{
			var remaining = Math.Min(1024, bytes.Length - offset);
			SHA256.HashData(bytes.AsSpan(offset, remaining), hash);
			for (var i = 0; i < hash.Length; i++)
			{
				var idx = i % dimension;
				vec[idx] += (hash[i] / 255f);
			}
			offset += remaining;
		}

		// L2 normalize
		var norm = MathF.Sqrt(vec.Sum(v => v * v));
		if (norm > 0)
		{
			for (var i = 0; i < vec.Length; i++)
			{
				vec[i] /= norm;
			}
		}
		return vec;
	}
}


