using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Domain.Extraction;

namespace LORE_LLM.Application.PostProcessing;

public sealed class MarbleNestPostProcessor : IPostExtractionProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private const string TargetProject = "pathologic2-marble-nest";

    public bool CanProcess(string sanitizedProject) => string.Equals(sanitizedProject, TargetProject, StringComparison.OrdinalIgnoreCase);

    public async Task<Result> ProcessAsync(PostProcessingContext context, CancellationToken cancellationToken)
    {
        var sourcePath = Path.Combine(context.ProjectDirectory.FullName, "source_text_raw.json");
        if (!File.Exists(sourcePath))
        {
            return Result.Success();
        }

        SourceTextRawDocument? document;
        await using (var stream = File.OpenRead(sourcePath))
        {
            document = await JsonSerializer.DeserializeAsync<SourceTextRawDocument>(stream, JsonOptions, cancellationToken);
        }

        if (document is null)
        {
            return Result.Failure("Failed to deserialize source_text_raw.json for post-processing.");
        }

        var filtered = document.Segments.Where(segment => !segment.IsEmpty).ToList();
        if (filtered.Count == document.Segments.Count)
        {
            return Result.Success();
        }

        var updatedDocument = new SourceTextRawDocument(
            document.SourcePath,
            document.GeneratedAt,
            document.Project,
            document.ProjectDisplayName,
            document.InputHash,
            filtered);

        await using (var outputStream = File.Create(sourcePath))
        {
            await JsonSerializer.SerializeAsync(outputStream, updatedDocument, JsonOptions, cancellationToken);
        }

        return Result.Success();
    }
}
