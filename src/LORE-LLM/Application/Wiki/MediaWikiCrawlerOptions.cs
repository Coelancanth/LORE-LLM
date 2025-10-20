using System.Collections.Generic;

namespace LORE_LLM.Application.Wiki;

public sealed class MediaWikiCrawlerProjectOptions
{
    public string ApiBase { get; set; } = string.Empty;

    public List<string> HtmlPostProcessors { get; } = new();

    public List<MediaWikiTabOutputOptions> TabOutputs { get; } = new();

    public bool EmitBaseDocument { get; set; } = true;
}

public sealed class MediaWikiCrawlerOptions
{
    private readonly Dictionary<string, MediaWikiCrawlerProjectOptions> _projects = new(StringComparer.OrdinalIgnoreCase);

    public IDictionary<string, MediaWikiCrawlerProjectOptions> Projects => _projects;

    public MediaWikiCrawlerProjectOptions? GetProjectOptions(string sanitizedProjectName) =>
        _projects.TryGetValue(sanitizedProjectName, out var options) ? options : null;
}

public sealed class MediaWikiTabOutputOptions
{
    public string TabName { get; set; } = string.Empty;

    public string? TabSlug { get; set; }

    public string? FileSuffix { get; set; }

    public string? TitleFormat { get; set; }
}
