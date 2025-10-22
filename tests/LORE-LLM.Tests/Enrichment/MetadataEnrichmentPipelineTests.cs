using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LORE_LLM.Application.Enrichment;
using LORE_LLM.Application.Enrichment.Enrichers;
using LORE_LLM.Domain.Extraction;
using Shouldly;
using Xunit;

namespace LORE_LLM.Tests.Enrichment;

public class MetadataEnrichmentPipelineTests
{
    [Fact]
    public async Task Applies_id_prefix_enricher()
    {
        var dir = CreateTempProjectDirectory();
        var doc = new SourceTextRawDocument(
            SourcePath: "raw-input/pathologic2-marble-nest/english.txt",
            GeneratedAt: System.DateTimeOffset.UtcNow,
            Project: "pathologic2-marble-nest",
            ProjectDisplayName: "Pathologic 2: Marble Nest",
            InputHash: new string('a', 64),
            Segments: new[] { new SourceSegment("conv:1", "Hello", false, 1) });
        await File.WriteAllTextAsync(Path.Combine(dir, "source_text_raw.json"), JsonSerializer.Serialize(doc, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var pipeline = new MetadataEnrichmentPipeline(new IMetadataEnricher[]
        {
            new IdPrefixEnricher(), new IdLookupEnricher(), new PathPatternEnricher()
        });

        var cfg = new MetadataEnrichmentConfig(
            IdPrefixRules: new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["conv:"] = new Dictionary<string, string> { ["category"] = "dialogue" }
            },
            IdLookupRules: null,
            PathPatternRules: new []
            {
                new PathPatternRule(Contains: "marble-nest", StartsWith: null, EndsWith: null, Metadata: new Dictionary<string, string>{{"source","mn"}})
            }
        );

        var result = await pipeline.RunAsync(new DirectoryInfo(dir), cfg, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();

        var json = await File.ReadAllTextAsync(Path.Combine(dir, "source_text_raw.json"));
        var updated = JsonSerializer.Deserialize<SourceTextRawDocument>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        updated!.Segments.Single().Metadata!.ContainsKey("category").ShouldBeTrue();
        updated!.Segments.Single().Metadata!["category"].ShouldBe("dialogue");
        updated!.Segments.Single().Metadata!["source"].ShouldBe("mn");
    }

    private static string CreateTempProjectDirectory()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "lore-llm-tests", "enrich", System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        return baseDir;
    }
}


