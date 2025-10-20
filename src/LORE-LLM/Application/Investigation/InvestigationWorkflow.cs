using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.Commands.Investigate;
using LORE_LLM.Application.PostProcessing;
using LORE_LLM.Domain.Extraction;

namespace LORE_LLM.Application.Investigation;

public sealed class InvestigationWorkflow : IInvestigationWorkflow
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly InvestigationReportGenerator _reportGenerator;
    private readonly IProjectNameSanitizer _projectNameSanitizer;

    public InvestigationWorkflow(
        InvestigationReportGenerator reportGenerator,
        IProjectNameSanitizer projectNameSanitizer)
    {
        _reportGenerator = reportGenerator;
        _projectNameSanitizer = projectNameSanitizer;
    }

    public async Task<Result<int>> RunAsync(InvestigationCommandOptions options, CancellationToken cancellationToken)
    {
        if (options.Workspace is null || !options.Workspace.Exists)
        {
            return Result.Failure<int>("Workspace directory not found.");
        }

        var sanitizedProject = _projectNameSanitizer.Sanitize(options.Project);
        var projectPath = Path.Combine(options.Workspace.FullName, sanitizedProject);
        var projectDirectory = new DirectoryInfo(projectPath);
        if (!projectDirectory.Exists)
        {
            return Result.Failure<int>($"Project directory not found: {projectDirectory.FullName}");
        }

        var sourceTextPath = Path.Combine(projectDirectory.FullName, "source_text_raw.json");
        if (!File.Exists(sourceTextPath))
        {
            return Result.Failure<int>($"Required artifact missing: {sourceTextPath}");
        }

        SourceTextRawDocument? sourceDocument;
        await using (var sourceStream = File.OpenRead(sourceTextPath))
        {
            sourceDocument = await JsonSerializer.DeserializeAsync<SourceTextRawDocument>(sourceStream, JsonOptions, cancellationToken);
        }

        if (sourceDocument is null)
        {
            return Result.Failure<int>("Failed to deserialize source_text_raw.json.");
        }

        var reportResult = await _reportGenerator.GenerateAsync(
            projectDirectory,
            sourceDocument,
            options.ForceRefresh,
            options.Offline,
            cancellationToken);
        if (reportResult.IsFailure)
        {
            return Result.Failure<int>(reportResult.Error);
        }

        var report = reportResult.Value;
        var investigationPath = Path.Combine(projectDirectory.FullName, "investigation.json");
        await using (var investigationStream = File.Create(investigationPath))
        {
            await JsonSerializer.SerializeAsync(investigationStream, report, JsonOptions, cancellationToken);
        }

        var manifestPath = Path.Combine(projectDirectory.FullName, "workspace.json");
        if (File.Exists(manifestPath))
        {
            await UpdateManifestAsync(projectDirectory, manifestPath, report.GeneratedAt, cancellationToken);
        }

        return Result.Success(0);
    }

    private static async Task UpdateManifestAsync(DirectoryInfo projectDirectory, string manifestPath, DateTimeOffset generatedAt, CancellationToken cancellationToken)
    {
        WorkspaceManifest? manifest;
        await using (var manifestStream = File.OpenRead(manifestPath))
        {
            manifest = await JsonSerializer.DeserializeAsync<WorkspaceManifest>(manifestStream, JsonOptions, cancellationToken);
        }

        if (manifest is null)
        {
            return;
        }

        var artifacts = new Dictionary<string, string>(manifest.Artifacts, StringComparer.OrdinalIgnoreCase)
        {
            ["investigationReport"] = "investigation.json"
        };

        var knowledgePath = Path.Combine(projectDirectory.FullName, "knowledge_base.json");
        if (File.Exists(knowledgePath))
        {
            artifacts["knowledgeBase"] = "knowledge_base.json";
        }

        var keywordIndexRelative = Path.Combine("knowledge", "wiki_keyword_index.json");
        var keywordIndexPath = Path.Combine(projectDirectory.FullName, keywordIndexRelative);
        if (File.Exists(keywordIndexPath))
        {
            artifacts["knowledgeKeywordIndex"] = keywordIndexRelative.Replace('\\', '/');
        }

        var updatedManifest = new WorkspaceManifest(
            generatedAt,
            manifest.Project,
            manifest.ProjectDisplayName,
            manifest.InputPath,
            manifest.InputHash,
            artifacts);

        await using var outputStream = File.Create(manifestPath);
        await JsonSerializer.SerializeAsync(outputStream, updatedManifest, JsonOptions, cancellationToken);
    }
}
