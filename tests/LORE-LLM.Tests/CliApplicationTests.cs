using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.Abstractions;
using LORE_LLM.Application.Investigation;
using LORE_LLM.Application.PostProcessing;
using LORE_LLM.Domain.Extraction;
using LORE_LLM.Domain.Investigation;
using LORE_LLM.Domain.Knowledge;
using LORE_LLM.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace LORE_LLM.Tests;

public class CliApplicationTests
{
    [Fact]
    public async Task RunAsync_without_arguments_returns_help_error_code()
    {
        var cli = CreateCliApplication();

        var exitCode = await cli.RunAsync(Array.Empty<string>());

        exitCode.ShouldBe(1);
    }

    public static IEnumerable<object[]> CommandScenarios()
    {
        yield return new object[] { BuildExtractScenario() };
        yield return new object[] { BuildSimpleScenario("augment", "--workspace", CreateTempDirectory()) };
        yield return new object[] { BuildSimpleScenario("translate", "--workspace", CreateTempDirectory(), "--language", "ru") };
        yield return new object[] { BuildSimpleScenario("validate", "--workspace", CreateTempDirectory()) };
        yield return new object[] { BuildSimpleScenario("integrate", "--workspace", CreateTempDirectory(), "--destination", CreateTempDirectory()) };
        yield return new object[] { BuildInvestigateScenario() };
    }

    [Theory]
    [MemberData(nameof(CommandScenarios))]
    public async Task Commands_return_success(CommandScenario scenario)
    {
        var cli = CreateCliApplication(scenario.ConfigureServices);

        var exitCode = await cli.RunAsync(scenario.Arguments);

        exitCode.ShouldBe(0);
        scenario.AssertPostConditions();
    }

    private static CommandScenario BuildExtractScenario()
    {
        var inputFile = CreateTempFile("1111 Sample line", "2222");
        var workspace = CreateTempDirectory();
        const string projectName = "pathologic2-marble-nest";

        var args = new[]
        {
            "extract",
            "--input", inputFile,
            "--output", workspace,
            "--project", projectName
        };

        return new CommandScenario(args, () =>
        {
            var projectFolder = Path.Combine(workspace, projectName);
            var rawPath = Path.Combine(projectFolder, "source_text_raw.json");
            var manifestPath = Path.Combine(projectFolder, "workspace.json");

            File.Exists(rawPath).ShouldBeTrue("Expected extractor to create source_text_raw.json");
            File.Exists(manifestPath).ShouldBeTrue("Expected extractor to create workspace.json");

            var document = JsonSerializer.Deserialize<SourceTextRawDocument>(
                File.ReadAllText(rawPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            document.ShouldNotBeNull();
            document!.Segments.ShouldNotBeEmpty();
            document.Segments.Any(segment => segment.IsEmpty).ShouldBeFalse("Post-processing should remove empty segments.");
        });
    }

    private static CommandScenario BuildSimpleScenario(params string[] arguments)
    {
        return new CommandScenario(arguments, () => { });
    }

    private static CommandScenario BuildInvestigateScenario()
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
            "investigate",
            "--workspace", workspace,
            "--project", projectDisplayName
        };

        return new CommandScenario(
            args,
            () =>
            {
                var investigationPath = Path.Combine(projectFolder, "investigation.json");
                File.Exists(investigationPath).ShouldBeTrue("Investigation report should be generated.");

                var report = JsonSerializer.Deserialize<InvestigationReport>(
                    File.ReadAllText(investigationPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                report.ShouldNotBeNull();
                report!.Suggestions.ShouldNotBeEmpty();
                report.Suggestions[0].Tokens.ShouldContain("Executor");

                var manifestPath = Path.Combine(projectFolder, "workspace.json");
                var updatedManifest = JsonSerializer.Deserialize<WorkspaceManifest>(
                    File.ReadAllText(manifestPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                updatedManifest.ShouldNotBeNull();
                updatedManifest!.Artifacts.ContainsKey("investigationReport").ShouldBeTrue();
            },
            services => services.AddSingleton<IMediaWikiIngestionService>(new StubMediaWikiIngestionService()));
    }

    private static ICliApplication CreateCliApplication(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLoreLlmServices();
        configure?.Invoke(services);
        return services.BuildServiceProvider().GetRequiredService<ICliApplication>();
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "lore-llm-tests", "cli", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateTempFile(params string[] lines)
    {
        var path = Path.Combine(Path.GetTempPath(), "lore-llm-tests", "cli", Guid.NewGuid().ToString("N") + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines);
        return path;
    }

    public sealed record CommandScenario(string[] Arguments, Action AssertPostConditions, Action<IServiceCollection>? ConfigureServices = null);

    private sealed class StubMediaWikiIngestionService : IMediaWikiIngestionService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public Task<Result<KnowledgeBaseDocument>> EnsureKnowledgeBaseAsync(
            DirectoryInfo projectDirectory,
            string sanitizedProject,
            string projectDisplayName,
            IEnumerable<string> candidateTokens,
            bool forceRefresh,
            bool offline,
            CancellationToken cancellationToken)
        {
            var entry = new KnowledgeEntry(
                ConceptId: "concept:executor",
                Title: "Executor",
                Summary: "Masked enforcer in Pathologic.",
                Source: new KnowledgeSource(
                    Provider: "pathologic.fandom.com",
                    Url: "https://pathologic.fandom.com/wiki/Executor",
                    License: "CC-BY-SA 3.0",
                    RetrievedAt: DateTimeOffset.UtcNow),
                Aliases: new List<string> { "Executor" });

            var document = new KnowledgeBaseDocument(
                Project: sanitizedProject,
                ProjectDisplayName: projectDisplayName,
                GeneratedAt: DateTimeOffset.UtcNow,
                Entries: new List<KnowledgeEntry> { entry });

            var knowledgePath = Path.Combine(projectDirectory.FullName, "knowledge_base.json");
            File.WriteAllText(knowledgePath, JsonSerializer.Serialize(document, JsonOptions));

            return Task.FromResult(Result.Success(document));
        }
    }
}

