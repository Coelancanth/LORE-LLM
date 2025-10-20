using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LORE_LLM.Application.Extraction;
using LORE_LLM.Application.PostProcessing;
using LORE_LLM.Domain.Extraction;
using Shouldly;
using Xunit;

namespace LORE_LLM.Tests.Extraction;

public class RawTextExtractorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task ExtractAsync_emits_json_and_manifest()
    {
        var extractor = new RawTextExtractor(new ProjectNameSanitizer());
        var inputFile = CreateInputFile(
            "1111 First line",
            "2222",
            "",
            "3333 Third line");
        var workspace = CreateTempDirectory();
        const string projectName = "Pathologic2 Marble Nest";

        var result = await extractor.ExtractAsync(new FileInfo(inputFile), new DirectoryInfo(workspace), projectName, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var sanitizedProject = "pathologic2-marble-nest";
        var projectFolder = Path.Combine(workspace, sanitizedProject);
        var rawPath = Path.Combine(projectFolder, "source_text_raw.json");
        var manifestPath = Path.Combine(projectFolder, "workspace.json");

        File.Exists(rawPath).ShouldBeTrue();
        File.Exists(manifestPath).ShouldBeTrue();

        SourceTextRawDocument? document;
        await using (var rawStream = File.OpenRead(rawPath))
        {
            document = await JsonSerializer.DeserializeAsync<SourceTextRawDocument>(rawStream, JsonOptions);
        }

        document.ShouldNotBeNull();
        document!.Project.ShouldBe(sanitizedProject);
        document.ProjectDisplayName.ShouldBe(projectName);
        document.Segments.Count.ShouldBe(3);
        document.Segments[0].Id.ShouldBe("1111");
        document.Segments[0].Text.ShouldBe("First line");
        document.Segments[0].IsEmpty.ShouldBeFalse();
        document.Segments[1].Id.ShouldBe("2222");
        document.Segments[1].IsEmpty.ShouldBeTrue();
        document.Segments[2].Id.ShouldBe("3333");

        WorkspaceManifest? manifest;
        await using (var manifestStream = File.OpenRead(manifestPath))
        {
            manifest = await JsonSerializer.DeserializeAsync<WorkspaceManifest>(manifestStream, JsonOptions);
        }

        manifest.ShouldNotBeNull();
        manifest!.Project.ShouldBe(sanitizedProject);
        manifest.ProjectDisplayName.ShouldBe(projectName);
        manifest.InputHash.ShouldBe(document.InputHash);
        manifest.Artifacts.TryGetValue("sourceTextRaw", out var artifactPath).ShouldBeTrue();
        artifactPath.ShouldBe("source_text_raw.json");
    }

    [Fact]
    public async Task ExtractAsync_fails_when_file_missing()
    {
        var extractor = new RawTextExtractor(new ProjectNameSanitizer());
        var workspace = CreateTempDirectory();

        var result = await extractor.ExtractAsync(new FileInfo(Path.Combine(workspace, "missing.txt")), new DirectoryInfo(workspace), "project", CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task ExtractAsync_fails_when_no_segments()
    {
        var extractor = new RawTextExtractor(new ProjectNameSanitizer());
        var inputFile = CreateInputFile("   ", "\t");
        var workspace = CreateTempDirectory();

        var result = await extractor.ExtractAsync(new FileInfo(inputFile), new DirectoryInfo(workspace), "project", CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
    }

    private static string CreateInputFile(params string[] lines)
    {
        var path = Path.Combine(Path.GetTempPath(), "lore-llm-tests", "input", Guid.NewGuid().ToString("N") + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines);
        return path;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "lore-llm-tests", "workspace", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}



