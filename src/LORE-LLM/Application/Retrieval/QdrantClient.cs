using System.Net.Http.Json;
using CSharpFunctionalExtensions;

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
}


