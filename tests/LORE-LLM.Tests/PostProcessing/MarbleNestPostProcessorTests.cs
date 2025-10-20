using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using LORE_LLM.Application.PostProcessing;
using LORE_LLM.Domain.Extraction;
using Shouldly;
using Xunit;

namespace LORE_LLM.Tests.PostProcessing;

public class MarbleNestPostProcessorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Removes_empty_segments()
    {
        var tempDir = CreateTempProjectDirectory();
        var context = new PostProcessingContext(
            ProjectDisplayName: "Pathologic 2: Marble Nest",
            SanitizedProjectName: "pathologic2-marble-nest",
            ProjectDirectory: new DirectoryInfo(tempDir));

        var document = new SourceTextRawDocument(
            SourcePath: "raw-input/english.txt",
            GeneratedAt: DateTimeOffset.UtcNow,
            Project: context.SanitizedProjectName,
            ProjectDisplayName: context.ProjectDisplayName,
            InputHash: "abc123",
            Segments: new List<SourceSegment>
            {
                new("conv:1", "Hello", false, 1),
                new("conv:2", string.Empty, true, 2)
            });

        var jsonPath = Path.Combine(tempDir, "source_text_raw.json");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(document, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));

        var processor = new MarbleNestPostProcessor();
        var result = await processor.ProcessAsync(context, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var updated = JsonSerializer.Deserialize<SourceTextRawDocument>(await File.ReadAllTextAsync(jsonPath), JsonOptions);
        updated.ShouldNotBeNull();
        updated!.Segments.Count.ShouldBe(1);
        updated.Segments[0].Id.ShouldBe("conv:1");
    }

    private static string CreateTempProjectDirectory()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "lore-llm-tests", "post-processing", Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(baseDir, "pathologic2-marble-nest");
        Directory.CreateDirectory(projectDir);
        return projectDir;
    }
}



