using LORE_LLM.Application.Abstractions;
using LORE_LLM.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Text.Json;
using Xunit;
using LORE_LLM.Domain.Knowledge;
using LORE_LLM.Application.PostProcessing;

namespace LORE_LLM.Tests.Wiki;

public class WikiIndexCommandTests
{
    [Fact]
    public async Task Index_wiki_builds_keyword_index_from_markdown_files()
    {
        var workspace = CreateTempDirectory();
        const string projectDisplayName = "Pathologic2 Marble Nest";
        var sanitizer = new ProjectNameSanitizer();
        var sanitized = sanitizer.Sanitize(projectDisplayName);
        var rawDir = Path.Combine(workspace, sanitized, "knowledge", "raw");
        Directory.CreateDirectory(rawDir);

        File.WriteAllText(Path.Combine(rawDir, "daniil-dankovsky.md"), "# Daniil Dankovsky\n\n> Source: ...");
        File.WriteAllText(Path.Combine(rawDir, "executor.md"), "# Executor\n\n> Source: ...\n\nRedirect to:\n\n- [Executioner](executioner.md)");
        File.WriteAllText(Path.Combine(rawDir, "backbone.md"), "# Backbone\n\n> Source: ...\n\nRedirect to:\n\n- [The Town](the-town.md)\n");

        var args = new[]
        {
            "index-wiki",
            "--workspace", workspace,
            "--project", projectDisplayName,
            "--force-refresh"
        };

        var cli = CreateCliApplication();
        var exitCode = await cli.RunAsync(args);
        exitCode.ShouldBe(0);

        var indexPath = Path.Combine(workspace, sanitized, "knowledge", "wiki_keyword_index.json");
        File.Exists(indexPath).ShouldBeTrue();

        var index = JsonSerializer.Deserialize<KnowledgeKeywordIndex>(
            File.ReadAllText(indexPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        index.ShouldNotBeNull();
        index!.Entries.Count.ShouldBeGreaterThan(0);
        var redirectEntry = index.Entries.FirstOrDefault(e => e.Title.Contains("executor", StringComparison.OrdinalIgnoreCase));
        redirectEntry.ShouldNotBeNull();
        redirectEntry!.IsRedirect.ShouldBeTrue();
        redirectEntry.RedirectTargets.ShouldNotBeNull();
        redirectEntry.RedirectTargets!.Count.ShouldBe(1);
        redirectEntry.RedirectTargets![0].Slug.ShouldBe("executioner");

        var backbone = index.Entries.FirstOrDefault(e => e.Title.Equals("backbone", StringComparison.OrdinalIgnoreCase));
        backbone.ShouldNotBeNull();
        backbone!.IsRedirect.ShouldBeTrue();
        backbone.RedirectTargets.ShouldNotBeNull();
        backbone.RedirectTargets![0].Slug.ShouldBe("the-town");
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


