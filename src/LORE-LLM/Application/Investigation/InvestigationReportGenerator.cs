using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Domain.Extraction;
using LORE_LLM.Domain.Investigation;
using LORE_LLM.Domain.Knowledge;

namespace LORE_LLM.Application.Investigation;

public sealed class InvestigationReportGenerator
{
    private readonly IMediaWikiIngestionService _mediaWikiIngestion;
    private readonly InvestigationMatcher _matcher;

    public InvestigationReportGenerator(IMediaWikiIngestionService mediaWikiIngestion)
    {
        _mediaWikiIngestion = mediaWikiIngestion;
        _matcher = new InvestigationMatcher();
    }

    public async Task<Result<InvestigationReport>> GenerateAsync(
        DirectoryInfo projectDirectory,
        SourceTextRawDocument sourceDocument,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var segmentContexts = BuildTokenContexts(sourceDocument.Segments);
        var aggregatedTokens = segmentContexts
            .SelectMany(context => context.Tokens)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var knowledgeResult = await _mediaWikiIngestion.EnsureKnowledgeBaseAsync(
            projectDirectory,
            sourceDocument.Project,
            sourceDocument.ProjectDisplayName,
            aggregatedTokens,
            forceRefresh,
            cancellationToken);

        if (knowledgeResult.IsFailure)
        {
            return Result.Failure<InvestigationReport>(knowledgeResult.Error);
        }

        var knowledge = knowledgeResult.Value;
        var suggestions = new List<InvestigationSuggestion>();

        foreach (var context in segmentContexts)
        {
            var matches = _matcher.Match(context.Segment.Text, context.Tokens, knowledge.Entries);
            if (matches.Count == 0)
            {
                continue;
            }

            var matchedTokens = matches
                .SelectMany(match => match.Tokens)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var candidates = matches
                .Select(match => match.Candidate)
                .ToList();

            suggestions.Add(new InvestigationSuggestion(context.Segment.Id, matchedTokens, candidates));
        }

        return Result.Success(new InvestigationReport(
            sourceDocument.Project,
            sourceDocument.ProjectDisplayName,
            DateTimeOffset.UtcNow,
            sourceDocument.InputHash,
            suggestions));
    }

    private static IReadOnlyList<SegmentTokenContext> BuildTokenContexts(IReadOnlyList<SourceSegment> segments)
    {
        var contexts = new List<SegmentTokenContext>();

        foreach (var segment in segments)
        {
            var tokens = InvestigationTokenExtractor.ExtractTokens(segment.Text);
            if (tokens.Count == 0)
            {
                continue;
            }

            contexts.Add(new SegmentTokenContext(segment, tokens));
        }

        return contexts;
    }

    private sealed record SegmentTokenContext(SourceSegment Segment, IReadOnlyList<string> Tokens);
}
