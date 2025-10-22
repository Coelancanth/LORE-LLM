using System.IO;
using System.Text.Json;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.Commands.Cluster;
using LORE_LLM.Application.PostProcessing;
using LORE_LLM.Application.Retrieval;
using LORE_LLM.Domain.Clusters;
using LORE_LLM.Domain.Extraction;

namespace LORE_LLM.Application.Clustering;

public interface IClusterContextWorkflow
{
	Task<Result<int>> RunAsync(ClusterContextCommandOptions options, CancellationToken cancellationToken);
}

public sealed class ClusterContextWorkflow : IClusterContextWorkflow
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true
	};

	private readonly IProjectNameSanitizer _projectNameSanitizer;
	private readonly VectorRetrievalOrchestrator _orchestrator;

	public ClusterContextWorkflow(IProjectNameSanitizer projectNameSanitizer, VectorRetrievalOrchestrator orchestrator)
	{
		_projectNameSanitizer = projectNameSanitizer;
		_orchestrator = orchestrator;
	}

	public async Task<Result<int>> RunAsync(ClusterContextCommandOptions options, CancellationToken cancellationToken)
	{
		if (!options.Workspace.Exists)
		{
			return Result.Failure<int>("Workspace directory not found.");
		}

		var sanitizedProject = _projectNameSanitizer.Sanitize(options.Project);
		var projectDirectory = new DirectoryInfo(Path.Combine(options.Workspace.FullName, sanitizedProject));
		if (!projectDirectory.Exists)
		{
			return Result.Failure<int>($"Project directory not found: {projectDirectory.FullName}");
		}

		var clustersPath = Path.Combine(projectDirectory.FullName, "clusters_llm.json");
		if (!File.Exists(clustersPath))
		{
			return Result.Failure<int>($"Required artifact missing: {clustersPath}");
		}

		var clusters = await DeserializeAsync<ClusterDocument>(clustersPath, cancellationToken);
		if (clusters is null)
		{
			return Result.Failure<int>("Failed to deserialize clusters_llm.json.");
		}

		var entries = new List<ClusterContextEntry>();
		foreach (var ctx in clusters.Clusters)
		{
			cancellationToken.ThrowIfCancellationRequested();
			// Query vector store using a simple query text: clusterId + shared context joined.
			var queryText = string.Join("\n", new[] { ctx.ClusterId }.Concat(ctx.SharedContext ?? Array.Empty<string>()));
			var search = await _orchestrator.SearchAsync(
				collection: "lore_llm_wiki",
				queryText: queryText,
				dimension: 384,
				topK: Math.Max(1, options.TopK),
				keywordFilters: ctx.KnowledgeReferences,
				cancellationToken: cancellationToken);
			if (search.IsFailure)
			{
				return Result.Failure<int>(search.Error);
			}

			var snippets = new List<KnowledgeSnippet>();
			foreach (var hit in search.Value)
			{
				var title = TryGetString(hit.Payload, "title") ?? hit.Id;
				var slug = TryGetString(hit.Payload, "slug") ?? "";
				var path = TryGetString(hit.Payload, "path") ?? "";
				if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(path))
				{
					continue;
				}
				snippets.Add(new KnowledgeSnippet(title, slug, Array.Empty<string>(), path));
			}

			entries.Add(new ClusterContextEntry(
				ctx.ClusterId,
				ctx.MemberIds ?? Array.Empty<string>(),
				null,
				snippets,
				null));
		}

		var output = new ClusterContextDocument(
			clusters.Project,
			clusters.ProjectDisplayName,
			DateTimeOffset.UtcNow,
			entries);

		var outputPath = Path.Combine(projectDirectory.FullName, "cluster_context.json");
		await using (var stream = File.Create(outputPath))
		{
			await JsonSerializer.SerializeAsync(stream, output, JsonOptions, cancellationToken);
		}

		return Result.Success(entries.Count);
	}

	private static string? TryGetString(IReadOnlyDictionary<string, object?>? payload, string key)
	{
		if (payload is null) return null;
		return payload.TryGetValue(key, out var val) ? val?.ToString() : null;
	}

	private static async Task<T?> DeserializeAsync<T>(string path, CancellationToken cancellationToken)
	{
		await using var stream = File.OpenRead(path);
		return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
	}
}


