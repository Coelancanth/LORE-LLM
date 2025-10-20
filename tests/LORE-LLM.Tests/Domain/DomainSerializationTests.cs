using System;
using System.Collections.Generic;
using System.Text.Json;
using LORE_LLM.Domain.Clusters;
using LORE_LLM.Domain.Investigation;
using LORE_LLM.Domain.Knowledge;
using LORE_LLM.Domain.Metadata;
using Shouldly;
using Xunit;

namespace LORE_LLM.Tests.Domain;

public class DomainSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Metadata_document_round_trips()
    {
        var original = new MetadataInferredDocument(
            Project: "pathologic2-marble-nest",
            ProjectDisplayName: "Pathologic 2: Marble Nest",
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceTextHash: "abc123",
            Segments: new List<SegmentMetadata>
            {
                new(
                    SegmentId: "conv:1",
                    Speaker: "Executor",
                    Tone: "ominous",
                    KnowledgeReferences: new List<string> { "character:executor" })
            });

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<MetadataInferredDocument>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized!.Project.ShouldBe(original.Project);
        deserialized.ProjectDisplayName.ShouldBe(original.ProjectDisplayName);
        deserialized.SourceTextHash.ShouldBe(original.SourceTextHash);
        deserialized.Segments.Count.ShouldBe(1);
        deserialized.Segments[0].SegmentId.ShouldBe(original.Segments[0].SegmentId);
        deserialized.Segments[0].KnowledgeReferences.ShouldBe(original.Segments[0].KnowledgeReferences);
    }

    [Fact]
    public void Cluster_document_round_trips()
    {
        var original = new ClusterDocument(
            Project: "pathologic2-marble-nest",
            ProjectDisplayName: "Pathologic 2: Marble Nest",
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceTextHash: "abc123",
            Clusters: new List<ClusterContext>
            {
                new(
                    ClusterId: "scene:executor",
                    MemberIds: new List<string> { "conv:1", "conv:2" },
                    SharedContext: new List<string> { "Finale conversation" },
                    Confidence: 0.75)
            });

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ClusterDocument>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized!.Clusters.Count.ShouldBe(1);
        deserialized.Clusters[0].ClusterId.ShouldBe(original.Clusters[0].ClusterId);
        deserialized.Clusters[0].MemberIds.ShouldBe(original.Clusters[0].MemberIds);
    }

    [Fact]
    public void Knowledge_base_round_trips()
    {
        var original = new KnowledgeBaseDocument(
            Project: "pathologic2-marble-nest",
            ProjectDisplayName: "Pathologic 2: Marble Nest",
            GeneratedAt: DateTimeOffset.UtcNow,
            Entries: new List<KnowledgeEntry>
            {
                new(
                    ConceptId: "character:daniil_dankovsky",
                    Title: "Daniil Dankovsky",
                    Summary: "Thanatologist studying death.",
                    Source: new KnowledgeSource(
                        Provider: "pathologic.fandom.com",
                        Url: "https://pathologic.fandom.com/wiki/Daniil_Dankovsky",
                        License: "CC-BY-SA 3.0",
                        RetrievedAt: DateTimeOffset.UtcNow),
                    Aliases: new List<string> { "Bachelor" },
                    Categories: new List<string> { "Characters" })
            });

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<KnowledgeBaseDocument>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized!.Entries.Count.ShouldBe(1);
        deserialized.Entries[0].ConceptId.ShouldBe(original.Entries[0].ConceptId);
        deserialized.Entries[0].Source.Url.ShouldBe(original.Entries[0].Source.Url);
    }

    [Fact]
    public void Investigation_report_round_trips()
    {
        var original = new InvestigationReport(
            Project: "pathologic2-marble-nest",
            ProjectDisplayName: "Pathologic 2: Marble Nest",
            GeneratedAt: DateTimeOffset.UtcNow,
            InputHash: "abc123",
            Suggestions: new List<InvestigationSuggestion>
            {
                new(
                    SegmentId: "conv:1",
                    Tokens: new List<string> { "Daniil" },
                    Candidates: new List<InvestigationCandidate>
                    {
                        new(
                            ConceptId: "character:daniil_dankovsky",
                            Source: "https://pathologic.fandom.com/wiki/Daniil_Dankovsky",
                            Confidence: 0.95,
                            Notes: "Exact name match")
                    })
            });

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<InvestigationReport>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized!.Suggestions.Count.ShouldBe(1);
        deserialized.Suggestions[0].Candidates[0].ConceptId.ShouldBe(original.Suggestions[0].Candidates[0].ConceptId);
    }
}
