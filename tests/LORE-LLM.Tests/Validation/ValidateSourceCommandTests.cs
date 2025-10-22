using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LORE_LLM.Application.Commands.ValidateSource;
using LORE_LLM.Application.Validation;
using LORE_LLM.Domain.Extraction;
using Shouldly;
using Xunit;

namespace LORE_LLM.Tests.Validation;

public class ValidateSourceCommandTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Validate_succeeds_on_valid_document()
    {
        var temp = CreateTempProjectDirectory();
        var doc = new SourceTextRawDocument(
            SourcePath: "raw-input/sample.txt",
            GeneratedAt: System.DateTimeOffset.UtcNow,
            Project: "proj",
            ProjectDisplayName: "Proj",
            InputHash: new string('a', 64),
            Segments: new[] { new SourceSegment("id1", "text", false, 1) });
        await File.WriteAllTextAsync(Path.Combine(temp, "source_text_raw.json"), JsonSerializer.Serialize(doc, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var validator = new SourceSchemaValidator();
        var handler = new ValidateSourceCommandHandler(validator);
        var result = await handler.HandleAsync(new ValidateSourceCommandOptions(new DirectoryInfo(Path.GetDirectoryName(temp)!), null), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    private static string CreateTempProjectDirectory()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "lore-llm-tests", "validate", System.Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(baseDir, "proj");
        Directory.CreateDirectory(projectDir);
        return projectDir;
    }
}


