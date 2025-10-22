using System.Net.Http.Json;
using CSharpFunctionalExtensions;
using System.Text.Json;

namespace LORE_LLM.Application.Retrieval;

public sealed class QdrantClient
{
	private readonly HttpClient _http;
	private readonly string _endpoint;
	private readonly string? _apiKey;

	public QdrantClient(HttpClient httpClient, string endpoint, string? apiKey)
	{
		_http = httpClient;
		_endpoint = endpoint.TrimEnd('/');
		_apiKey = apiKey;
		if (!string.IsNullOrWhiteSpace(_apiKey))
		{
			_http.DefaultRequestHeaders.TryAddWithoutValidation("api-key", _apiKey);
		}
	}

	public async Task<Result> EnsureCollectionAsync(string collection, int vectors, CancellationToken cancellationToken)
	{
		var url = $"{_endpoint}/collections/{collection}";
		var get = await _http.GetAsync(url, cancellationToken);
		if (get.IsSuccessStatusCode)
		{
			return Result.Success();
		}

		var payload = new
		{
			vectors = new { size = vectors, distance = "Cosine" }
		};
		var resp = await _http.PutAsJsonAsync(url, payload, cancellationToken);
		return resp.IsSuccessStatusCode ? Result.Success() : Result.Failure("Failed to create Qdrant collection");
	}

	public async Task<Result> UpsertPointsAsync(string collection, IReadOnlyList<(string Id, float[] Vector, object? Payload)> points, CancellationToken cancellationToken)
	{
		if (points.Count == 0)
		{
			return Result.Success();
		}

		var url = $"{_endpoint}/collections/{collection}/points";
		var body = new
		{
			points = points.Select(p => new
			{
				id = p.Id,
				vector = p.Vector,
				payload = p.Payload
			}).ToArray()
		};
		var resp = await _http.PutAsJsonAsync(url, body, cancellationToken);
		return resp.IsSuccessStatusCode ? Result.Success() : Result.Failure("Failed to upsert points to Qdrant");
	}

	public async Task<Result<IReadOnlyList<QdrantSearchHit>>> SearchAsync(
		string collection,
		float[] vector,
		int topK,
		IEnumerable<string>? keywords,
		CancellationToken cancellationToken)
	{
		var url = $"{_endpoint}/collections/{collection}/points/search";
		object body;
		var kws = keywords?.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
		if (kws.Length > 0)
		{
			body = new
			{
				vector = vector,
				limit = topK,
				with_payload = true,
				filter = new
				{
					must = new object[]
					{
						new { key = "tokens", match = new { any = kws } }
					}
				}
			};
		}
		else
		{
			body = new
			{
				vector = vector,
				limit = topK,
				with_payload = true
			};
		}

		var resp = await _http.PostAsJsonAsync(url, body, cancellationToken);
		if (!resp.IsSuccessStatusCode)
		{
			return Result.Failure<IReadOnlyList<QdrantSearchHit>>("Qdrant search failed");
		}

		await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
		using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
		if (!doc.RootElement.TryGetProperty("result", out var resultEl) || resultEl.ValueKind != JsonValueKind.Array)
		{
			return Result.Success<IReadOnlyList<QdrantSearchHit>>(Array.Empty<QdrantSearchHit>());
		}

		var hits = new List<QdrantSearchHit>();
		foreach (var item in resultEl.EnumerateArray())
		{
			var id = item.TryGetProperty("id", out var idEl) ? idEl.ToString() : string.Empty;
			var score = item.TryGetProperty("score", out var scoreEl) && scoreEl.TryGetDouble(out var s) ? s : 0;
			Dictionary<string, object?>? payloadDict = null;
			if (item.TryGetProperty("payload", out var payloadEl) && payloadEl.ValueKind == JsonValueKind.Object)
			{
				payloadDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
				foreach (var prop in payloadEl.EnumerateObject())
				{
					payloadDict[prop.Name] = prop.Value.ValueKind switch
					{
						JsonValueKind.String => prop.Value.GetString(),
						JsonValueKind.Number => prop.Value.TryGetDouble(out var dv) ? dv : null,
						JsonValueKind.True => true,
						JsonValueKind.False => false,
						JsonValueKind.Array => prop.Value.EnumerateArray().Select(v => v.ToString()).ToArray(),
						_ => null
					};
				}
			}
			hits.Add(new QdrantSearchHit(id, score, payloadDict));
		}

		// Order by score descending just in case
		var ordered = hits.OrderByDescending(h => h.Score).ToList();
		return Result.Success<IReadOnlyList<QdrantSearchHit>>(ordered);
	}
}

public sealed record QdrantSearchHit(string Id, double Score, IReadOnlyDictionary<string, object?>? Payload);


