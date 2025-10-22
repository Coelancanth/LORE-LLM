using System;
using System.Collections.Generic;

namespace LORE_LLM.Domain.Clusters;

public sealed record ClusterContextDocument(
	string Project,
	string ProjectDisplayName,
	DateTimeOffset GeneratedAt,
	IReadOnlyList<ClusterContextEntry> Entries);

public sealed record ClusterContextEntry(
	string ClusterId,
	IReadOnlyList<string> SegmentIds,
	string? Category,
	IReadOnlyList<KnowledgeSnippet> KnowledgeSnippets,
	TranslationNotes? TranslationNotes);

public sealed record KnowledgeSnippet(
	string Title,
	string Slug,
	IReadOnlyList<string> Summary,
	string SourcePath);

public sealed record TranslationNotes(
	string? Tone,
	IReadOnlyDictionary<string, string>? SpeakerVoices,
	IReadOnlyList<string>? CulturalAdaptation);


