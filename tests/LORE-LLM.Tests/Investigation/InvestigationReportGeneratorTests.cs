using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.Investigation;
using LORE_LLM.Domain.Extraction;
using LORE_LLM.Domain.Knowledge;
using Shouldly;
using Xunit;

namespace LORE_LLM.Tests.Investigation;

public class InvestigationReportGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_produces_suggestions_from_knowledge()
    {
        var knowledge = new KnowledgeBaseDocument(
            "pathologic2-marble-nest",
            "Pathologic2 Marble Nest",
            DateTimeOffset.UtcNow,
            new List<KnowledgeEntry>
            {
                new(
                    "concept:executor",
                    "Executor",
                    "Masked enforcer in Pathologic.",
                    new KnowledgeSource(
                        "pathologic.fandom.com",
                        "https://pathologic.fandom.com/wiki/Executor",
                        "CC-BY-SA 3.0",
                        DateTimeOffset.UtcNow),
                    new List<string> { "Executor" })
            });

        var mediaWiki = new StubMediaWikiIngestionService(knowledge);
        var generator = new InvestigationReportGenerator(mediaWiki);

        var document = new SourceTextRawDocument(
            "input.txt",
            DateTimeOffset.UtcNow,
            "pathologic2-marble-nest",
            "Pathologic2 Marble Nest",
            "abc123",
            new List<SourceSegment>
            {
                new("seg-1", "The Executor stands before you.", false, 1),
                new("seg-2", "there is nothing to see here.", false, 2)
            });

        var result = await generator.GenerateAsync(new DirectoryInfo(Path.GetTempPath()), document, forceRefresh: false, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var report = result.Value;

        report.Suggestions.Count.ShouldBe(1);
        var suggestion = report.Suggestions[0];
        suggestion.SegmentId.ShouldBe("seg-1");
        suggestion.Tokens.ShouldContain("Executor");
        suggestion.Candidates.Count.ShouldBe(1);
        suggestion.Candidates[0].ConceptId.ShouldBe("concept:executor");
    }

    private sealed class StubMediaWikiIngestionService : IMediaWikiIngestionService
    {
        private readonly KnowledgeBaseDocument _document;

        public StubMediaWikiIngestionService(KnowledgeBaseDocument document)
        {
            _document = document;
        }

        public Task<Result<KnowledgeBaseDocument>> EnsureKnowledgeBaseAsync(
            DirectoryInfo projectDirectory,
            string sanitizedProject,
            string projectDisplayName,
            IEnumerable<string> candidateTokens,
            bool forceRefresh,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Success(_document));
        }
    }
}
