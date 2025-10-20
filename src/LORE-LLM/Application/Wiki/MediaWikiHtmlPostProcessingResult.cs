using System.Collections.Generic;

namespace LORE_LLM.Application.Wiki;

public sealed class MediaWikiHtmlPostProcessingResult
{
    public MediaWikiHtmlPostProcessingResult(
        string html,
        IReadOnlyList<MediaWikiTabSection> tabSections,
        IReadOnlyList<MediaWikiRedirectTarget> redirectTargets)
    {
        Html = html;
        TabSections = tabSections;
        RedirectTargets = redirectTargets;
    }

    public string Html { get; }

    public IReadOnlyList<MediaWikiTabSection> TabSections { get; }

    public IReadOnlyList<MediaWikiRedirectTarget> RedirectTargets { get; }
}

public sealed class MediaWikiTabSection
{
    public MediaWikiTabSection(string name, string slug, string html)
    {
        Name = name;
        Slug = slug;
        Html = html;
    }

    public string Name { get; }

    public string Slug { get; }

    public string Html { get; }
}

public sealed class MediaWikiRedirectTarget
{
    public MediaWikiRedirectTarget(string displayText, string href)
    {
        DisplayText = displayText;
        Href = href;
    }

    public string DisplayText { get; }

    public string Href { get; }
}
