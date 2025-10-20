using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.PostProcessing;
using LORE_LLM.Domain.Extraction;

namespace LORE_LLM.Application.Extraction;

public sealed class RawTextExtractor : IRawTextExtractor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IProjectNameSanitizer _projectNameSanitizer;

    public RawTextExtractor(IProjectNameSanitizer projectNameSanitizer)
    {
        _projectNameSanitizer = projectNameSanitizer;
    }

    public async Task<Result<int>> ExtractAsync(FileInfo inputFile, DirectoryInfo workspace, string project, CancellationToken cancellationToken)
    {
        if (!inputFile.Exists)
        {
            return Result.Failure<int>($"Input file not found: {inputFile.FullName}");
        }

        var sanitizedProject = _projectNameSanitizer.Sanitize(project);
        var projectDirectory = Path.Combine(workspace.FullName, sanitizedProject);
        Directory.CreateDirectory(projectDirectory);

        var segments = new List<SourceSegment>();
        await foreach (var segment in ReadSegmentsAsync(inputFile, cancellationToken))
        {
            if (segment is not null)
            {
                segments.Add(segment);
            }
        }

        if (segments.Count == 0)
        {
            return Result.Failure<int>("Input file did not contain any segments.");
        }

        var inputHash = await ComputeSha256Async(inputFile, cancellationToken);
        var generatedAt = DateTimeOffset.UtcNow;

        var document = new SourceTextRawDocument(
            SourcePath: Path.GetFullPath(inputFile.FullName),
            GeneratedAt: generatedAt,
            Project: sanitizedProject,
            ProjectDisplayName: project,
            InputHash: inputHash,
            Segments: segments);

        var documentPath = Path.Combine(projectDirectory, "source_text_raw.json");
        await using (var stream = File.Create(documentPath))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
        }

        var manifest = new WorkspaceManifest(
            GeneratedAt: generatedAt,
            Project: sanitizedProject,
            ProjectDisplayName: project,
            InputPath: Path.GetFullPath(inputFile.FullName),
            InputHash: inputHash,
            Artifacts: new Dictionary<string, string>
            {
                ["sourceTextRaw"] = "source_text_raw.json"
            });

        var manifestPath = Path.Combine(projectDirectory, "workspace.json");
        await using (var stream = File.Create(manifestPath))
        {
            await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken);
        }

        return Result.Success(0);
    }

    private static async IAsyncEnumerable<SourceSegment?> ReadSegmentsAsync(FileInfo file, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = file.OpenRead();
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var lineNumber = 0;
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rawLine = await reader.ReadLineAsync();
            lineNumber++;

            if (rawLine is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var trimmed = rawLine.TrimEnd();
            var firstWhitespace = trimmed.IndexOf(' ');

            string id;
            string text;

            if (firstWhitespace < 0)
            {
                id = trimmed;
                text = string.Empty;
            }
            else
            {
                id = trimmed[..firstWhitespace];
                text = trimmed[(firstWhitespace + 1)..];
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            yield return new SourceSegment(
                Id: id,
                Text: text,
                IsEmpty: string.IsNullOrWhiteSpace(text),
                LineNumber: lineNumber);
        }
    }

    private static async Task<string> ComputeSha256Async(FileInfo file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenRead();
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
