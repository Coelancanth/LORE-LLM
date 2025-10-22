using LORE_LLM.Application.Abstractions;
using LORE_LLM.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Text.Json;
using Xunit;
using System.Net;
using System.Net.Http;
using System.Text;

namespace LORE_LLM.Tests.Clustering;

public class ClusterContextCommandTests
{
	[Fact]
	public async Task Cluster_context_writes_output_file()
	{
		var workspace = CreateTempDirectory();
		const string projectDisplayName = "Pathologic2 Marble Nest";
		var sanitizer = new LORE_LLM.Application.PostProcessing.ProjectNameSanitizer();
		var sanitized = sanitizer.Sanitize(projectDisplayName);
		var projectDir = Path.Combine(workspace, sanitized);
		Directory.CreateDirectory(projectDir);

		// Minimal clusters_llm.json
		var clustersDoc = new
		{
			project = sanitized,
			projectDisplayName,
			generatedAt = DateTimeOffset.UtcNow,
			sourceTextHash = "abc",
			clusters = new[]
			{
				new { clusterId = "scene:test", memberIds = new[] { "seg:1" }, sharedContext = new[] { "Test" } }
			}
		};
		await File.WriteAllTextAsync(Path.Combine(projectDir, "clusters_llm.json"), JsonSerializer.Serialize(clustersDoc));

		var args = new[]
		{
			"cluster-context",
			"--workspace", workspace,
			"--project", projectDisplayName,
			"--top-k", "1"
		};

		var cli = CreateCliApplication();
		var exitCode = await cli.RunAsync(args);
		exitCode.ShouldBe(0);

		var outputPath = Path.Combine(projectDir, "cluster_context.json");
		File.Exists(outputPath).ShouldBeTrue();
	}

	private static ICliApplication CreateCliApplication()
	{
		var services = new ServiceCollection();
		services.AddLoreLlmServices();
		// Override Qdrant client with a stubbed HTTP handler to avoid external dependency.
		var handler = new QdrantStubHandler();
		var http = new HttpClient(handler);
		services.AddSingleton(new LORE_LLM.Application.Retrieval.QdrantClient(http, "http://localhost:6333", null));
		return services.BuildServiceProvider().GetRequiredService<ICliApplication>();
	}

	private static string CreateTempDirectory()
	{
		var path = Path.Combine(Path.GetTempPath(), "lore-llm-tests", "cli", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}

	private sealed class QdrantStubHandler : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var responseJson = "{\"result\":[{\"id\":\"executor\",\"score\":0.9,\"payload\":{\"title\":\"Executor\",\"slug\":\"executor\",\"path\":\"knowledge/raw/executor.md\",\"tokens\":[\"executor\"]}}]}";
			var resp = new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
			};
			return Task.FromResult(resp);
		}
	}
}


