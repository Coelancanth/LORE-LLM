using System.Text.Json;
using CSharpFunctionalExtensions;
using LORE_LLM.Domain.Extraction;
using LORE_LLM.Application.PostProcessing;

namespace LORE_LLM.Application.Validation;

public sealed class SourceSchemaValidator : ISourceSchemaValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<Result> ValidateAsync(DirectoryInfo workspace, string? project, CancellationToken cancellationToken)
    {
        if (!workspace.Exists)
        {
            return Result.Failure($"Workspace directory not found: {workspace.FullName}");
        }

        string projectFolderPath;
        if (!string.IsNullOrWhiteSpace(project))
        {
            var sanitized = new ProjectNameSanitizer().Sanitize(project);
            projectFolderPath = Path.Combine(workspace.FullName, sanitized);
        }
        else
        {
            // If project not specified, attempt single folder auto-detect
            var dirs = Directory.GetDirectories(workspace.FullName);
            if (dirs.Length != 1)
            {
                return Result.Failure("Workspace must contain exactly one project folder or specify --project.");
            }
            projectFolderPath = dirs[0];
        }

        var sourcePath = Path.Combine(projectFolderPath, "source_text_raw.json");
        if (!File.Exists(sourcePath))
        {
            return Result.Failure($"Missing source_text_raw.json at {sourcePath}");
        }

        try
        {
            await using var stream = File.OpenRead(sourcePath);
            var document = await JsonSerializer.DeserializeAsync<SourceTextRawDocument>(stream, JsonOptions, cancellationToken);
            if (document is null)
            {
                return Result.Failure("Unable to parse source_text_raw.json");
            }

            if (string.IsNullOrWhiteSpace(document.Project) || document.Segments is null)
            {
                return Result.Failure("Invalid document: missing project or segments");
            }

            foreach (var segment in document.Segments)
            {
                if (string.IsNullOrWhiteSpace(segment.Id))
                {
                    return Result.Failure("Invalid segment: id is required");
                }
                if (segment.LineNumber <= 0)
                {
                    return Result.Failure($"Invalid segment {segment.Id}: lineNumber must be >= 1");
                }
                // Metadata bag is optional; enforce string values only if present
                if (segment.Metadata is not null)
                {
                    // No-op: type enforced by strong typing
                }
            }
        }
        catch (JsonException ex)
        {
            return Result.Failure($"JSON parse error: {ex.Message}");
        }

        return Result.Success();
    }
}


