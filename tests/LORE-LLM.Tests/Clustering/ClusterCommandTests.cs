using System.Text.Json;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.Abstractions;
using LORE_LLM.Application.Chat;
using LORE_LLM.Domain.Clusters;
using LORE_LLM.Domain.Extraction;
using LORE_LLM.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using LORE_LLM.Application.PostProcessing;

namespace LORE_LLM.Tests.Clustering;

public class ClusterCommandTests
{
    [Fact]
    public async Task Cluster_command_generates_clusters_and_updates_manifest()
    {
        const string projectDisplayName = "Pathologic2 Marble Nest";
        var sanitizer = new ProjectNameSanitizer();
        var sanitizedProject = sanitizer.Sanitize(projectDisplayName);
        var workspace = CreateTempDirectory();
        var projectFolder = Path.Combine(workspace, sanitizedProject);
        Directory.CreateDirectory(projectFolder);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var sourceDocument = new SourceTextRawDocument(
            SourcePath: "input.txt",
            GeneratedAt: DateTimeOffset.UtcNow,
            Project: sanitizedProject,
            ProjectDisplayName: projectDisplayName,
            InputHash: "abc123",
            Segments: new List<SourceSegment>
            {
                new("seg-1", "Executor, stay your blade.", false, 1),
                new("seg-2", "Good evening.", false, 2)
            });

        File.WriteAllText(
            Path.Combine(projectFolder, "source_text_raw.json"),
            JsonSerializer.Serialize(sourceDocument, jsonOptions));

        var manifest = new WorkspaceManifest(
            GeneratedAt: DateTimeOffset.UtcNow,
            Project: sanitizedProject,
            ProjectDisplayName: projectDisplayName,
            InputPath: "input.txt",
            InputHash: sourceDocument.InputHash,
            Artifacts: new Dictionary<string, string>
            {
                ["sourceTextRaw"] = "source_text_raw.json"
            });

        File.WriteAllText(
            Path.Combine(projectFolder, "workspace.json"),
            JsonSerializer.Serialize(manifest, jsonOptions));

        var args = new[]
        {
            "cluster",
            "--workspace", workspace,
            "--project", projectDisplayName,
            "--provider", "local",
            "--batch-size", "1",
            "--max-segments", "1",
            "--save-transcript"
        };

        var cli = CreateCliApplication();
        var exitCode = await cli.RunAsync(args);
        exitCode.ShouldBe(0);

        var clustersPath = Path.Combine(projectFolder, "clusters_llm.json");
        File.Exists(clustersPath).ShouldBeTrue();
        var doc = JsonSerializer.Deserialize<ClusterDocument>(
            File.ReadAllText(clustersPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        doc.ShouldNotBeNull();
        doc!.Clusters.ShouldNotBeEmpty();
        doc.Clusters[0].MemberIds.ShouldContain("seg-1");

        var transcriptPath = Path.Combine(projectFolder, "clusters_llm_transcript.md");
        File.Exists(transcriptPath).ShouldBeTrue();

        var manifestPath = Path.Combine(projectFolder, "workspace.json");
        var updatedManifest = JsonSerializer.Deserialize<WorkspaceManifest>(
            File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        updatedManifest.ShouldNotBeNull();
        updatedManifest!.Artifacts.ContainsKey("clustersLlm").ShouldBeTrue();
    }

    [Fact]
    public async Task Cluster_command_accepts_precomputed_when_flag_is_accept()
    {
        const string projectDisplayName = "Pathologic2 Marble Nest";
        var sanitizer = new ProjectNameSanitizer();
        var sanitizedProject = sanitizer.Sanitize(projectDisplayName);
        var workspace = CreateTempDirectory();
        var projectFolder = Path.Combine(workspace, sanitizedProject);
        Directory.CreateDirectory(projectFolder);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var sourceDocument = new SourceTextRawDocument(
            SourcePath: "input.txt",
            GeneratedAt: DateTimeOffset.UtcNow,
            Project: sanitizedProject,
            ProjectDisplayName: projectDisplayName,
            InputHash: "abc123",
            Segments: new List<SourceSegment>
            {
                new("seg-1", "A", false, 1),
                new("seg-2", "B", false, 2)
            });

        File.WriteAllText(
            Path.Combine(projectFolder, "source_text_raw.json"),
            JsonSerializer.Serialize(sourceDocument, jsonOptions));

        var manifest = new WorkspaceManifest(
            GeneratedAt: DateTimeOffset.UtcNow,
            Project: sanitizedProject,
            ProjectDisplayName: projectDisplayName,
            InputPath: "input.txt",
            InputHash: sourceDocument.InputHash,
            Artifacts: new Dictionary<string, string>
            {
                ["sourceTextRaw"] = "source_text_raw.json"
            });

        File.WriteAllText(
            Path.Combine(projectFolder, "workspace.json"),
            JsonSerializer.Serialize(manifest, jsonOptions));

        var pre = new ClusterDocument(
            Project: sanitizedProject,
            ProjectDisplayName: projectDisplayName,
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceTextHash: sourceDocument.InputHash,
            Clusters: new List<ClusterContext>
            {
                new("pre:group", new List<string>{"seg-1","seg-2"})
            });
        File.WriteAllText(Path.Combine(projectFolder, "clusters_precomputed.json"), JsonSerializer.Serialize(pre, jsonOptions));

        var args = new[]
        {
            "cluster",
            "--workspace", workspace,
            "--project", projectDisplayName,
            "--provider", "local",
            "--precomputed", "accept"
        };

        var cli = CreateCliApplication();
        var exitCode = await cli.RunAsync(args);
        exitCode.ShouldBe(0);

        var clustersPath = Path.Combine(projectFolder, "clusters_llm.json");
        File.Exists(clustersPath).ShouldBeTrue();
        var doc = JsonSerializer.Deserialize<ClusterDocument>(
            File.ReadAllText(clustersPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        doc.ShouldNotBeNull();
        doc!.Clusters.Count.ShouldBe(1);
        doc.Clusters[0].MemberIds.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Cluster_command_uses_default_provider_from_config_when_not_specified()
    {
        // Arrange: ensure no provider flag provided, config defaultProvider is local via repo file
        const string projectDisplayName = "Pathologic2 Marble Nest";
        var sanitizer = new ProjectNameSanitizer();
        var sanitizedProject = sanitizer.Sanitize(projectDisplayName);
        var workspace = CreateTempDirectory();
        var projectFolder = Path.Combine(workspace, sanitizedProject);
        Directory.CreateDirectory(projectFolder);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var sourceDocument = new SourceTextRawDocument(
            SourcePath: "input.txt",
            GeneratedAt: DateTimeOffset.UtcNow,
            Project: sanitizedProject,
            ProjectDisplayName: projectDisplayName,
            InputHash: "abc123",
            Segments: new List<SourceSegment>
            {
                new("seg-1", "Executor, stay your blade.", false, 1)
            });

        File.WriteAllText(
            Path.Combine(projectFolder, "source_text_raw.json"),
            JsonSerializer.Serialize(sourceDocument, jsonOptions));

        var args = new[]
        {
            "cluster",
            "--workspace", workspace,
            "--project", projectDisplayName,
            "--batch-size", "1",
            "--max-segments", "1"
        };

        var cli = CreateCliApplication();
        var exitCode = await cli.RunAsync(args);
        exitCode.ShouldBe(0);
    }

    private static ICliApplication CreateCliApplication()
    {
        var services = new ServiceCollection();
        services.AddLoreLlmServices();
        // Ensure local provider exists
        services.AddSingleton<IChatProvider, LocalChatProvider>();
        return services.BuildServiceProvider().GetRequiredService<ICliApplication>();
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "lore-llm-tests", "cli", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}


