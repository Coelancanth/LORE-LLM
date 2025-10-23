using LORE_LLM.Application.Abstractions;
using LORE_LLM.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Text.Json;
using Xunit;

namespace LORE_LLM.Tests.Clustering;

public class ClusterPrepCommandTests
{
    [Fact]
    public async Task Cluster_prep_writes_precomputed_and_manifest()
    {
        const string projectDisplayName = "Heads Will Roll Reforged";
        var sanitizer = new LORE_LLM.Application.PostProcessing.ProjectNameSanitizer();
        var sanitized = sanitizer.Sanitize(projectDisplayName);
        var workspace = CreateTempDirectory();
        var projectDir = Path.Combine(workspace, sanitized);
        Directory.CreateDirectory(projectDir);

        // Minimal source_text_raw.json with deterministic metadata
        var doc = new
        {
            sourcePath = "raw",
            generatedAt = DateTimeOffset.UtcNow,
            project = sanitized,
            projectDisplayName,
            inputHash = "abc",
            segments = new[]
            {
                new { id = "f.rpy:block:1:a", text = "Hi", isEmpty = false, lineNumber = 1, metadata = new { sourceRelPath = "f.rpy", translationBlock = "block", blockInstance = "block:1", entryType = "character_line", speaker = "a" } },
                new { id = "f.rpy:block:1:b", text = "Yo", isEmpty = false, lineNumber = 2, metadata = new { sourceRelPath = "f.rpy", translationBlock = "block", blockInstance = "block:1", entryType = "character_line", speaker = "b" } },
                new { id = "f.rpy:block:1:a2", text = "...", isEmpty = false, lineNumber = 3, metadata = new { sourceRelPath = "f.rpy", translationBlock = "block", blockInstance = "block:1", entryType = "character_line", speaker = "a" } }
            }
        };
        await File.WriteAllTextAsync(Path.Combine(projectDir, "source_text_raw.json"), JsonSerializer.Serialize(doc));

        var manifest = new
        {
            generatedAt = DateTimeOffset.UtcNow,
            project = sanitized,
            projectDisplayName,
            inputPath = "raw",
            inputHash = "abc",
            artifacts = new { sourceTextRaw = "source_text_raw.json" }
        };
        await File.WriteAllTextAsync(Path.Combine(projectDir, "workspace.json"), JsonSerializer.Serialize(manifest));

        var args = new[]
        {
            "cluster-prep",
            "--workspace", workspace,
            "--project", projectDisplayName
        };

        var cli = CreateCliApplication();
        var exitCode = await cli.RunAsync(args);
        exitCode.ShouldBe(0);

        var prePath = Path.Combine(projectDir, "clusters_precomputed.json");
        File.Exists(prePath).ShouldBeTrue();

        var manifestPath = Path.Combine(projectDir, "workspace.json");
        var updated = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        updated.RootElement.GetProperty("artifacts").TryGetProperty("clustersPrecomputed", out var _).ShouldBeTrue();
    }

    private static ICliApplication CreateCliApplication()
    {
        var services = new ServiceCollection();
        services.AddLoreLlmServices();
        return services.BuildServiceProvider().GetRequiredService<ICliApplication>();
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "lore-llm-tests", "cli", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}


