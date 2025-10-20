using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;
using LORE_LLM.Application.Investigation;

namespace LORE_LLM.Application.Wiki;

public sealed class MediaWikiHtmlPostProcessingPipeline
{
    private readonly IReadOnlyDictionary<string, IMediaWikiHtmlPostProcessor> _processorMap;

    public MediaWikiHtmlPostProcessingPipeline(
        IEnumerable<IMediaWikiHtmlPostProcessor> processors)
    {
        _processorMap = processors.ToDictionary(processor => processor.Id, processor => processor, StringComparer.OrdinalIgnoreCase);
    }

    public MediaWikiHtmlPostProcessingResult Process(
        string sanitizedProjectName,
        string pageTitle,
        string html,
        IReadOnlyList<string>? configuredProcessors)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return new MediaWikiHtmlPostProcessingResult(
                html,
                Array.Empty<MediaWikiTabSection>(),
                Array.Empty<MediaWikiRedirectTarget>());
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);

        IEnumerable<IMediaWikiHtmlPostProcessor> processorsToRun;
        if (configuredProcessors is { Count: > 0 })
        {
            processorsToRun = configuredProcessors
                .Select(id => _processorMap.TryGetValue(id, out var processor) ? processor : null)
                .Where(processor => processor is not null)
                .Select(processor => processor!)
                .ToList();
        }
        else
        {
            processorsToRun = _processorMap.Values
                .Where(processor => processor.CanProcess(sanitizedProjectName))
                .ToList();
        }

        foreach (var processor in processorsToRun)
        {
            if (!processor.CanProcess(sanitizedProjectName))
            {
                continue;
            }

            processor.Process(sanitizedProjectName, pageTitle, document);
        }

        var redirectTargets = ExtractRedirectTargets(document);
        var tabSections = ExtractTabSections(document);
        var sanitizedHtml = document.DocumentNode.InnerHtml;
        return new MediaWikiHtmlPostProcessingResult(sanitizedHtml, tabSections, redirectTargets);
    }

    private static IReadOnlyList<MediaWikiTabSection> ExtractTabSections(HtmlDocument document)
    {
        var nodes = document.DocumentNode.SelectNodes("//*[@data-lore-tab-name]");
        if (nodes is null)
        {
            return Array.Empty<MediaWikiTabSection>();
        }

        var sections = new List<MediaWikiTabSection>(nodes.Count);
        foreach (var node in nodes)
        {
            var tabName = node.GetAttributeValue("data-lore-tab-name", string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(tabName))
            {
                node.Attributes.Remove("data-lore-tab-name");
                node.Attributes.Remove("data-lore-tab-slug");
                continue;
            }

            var tabSlug = node.GetAttributeValue("data-lore-tab-slug", string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(tabSlug))
            {
                tabSlug = TextSlugger.ToSlug(tabName);
            }

            var innerHtml = node.InnerHtml;
            sections.Add(new MediaWikiTabSection(tabName, tabSlug, innerHtml));

            node.Attributes.Remove("data-lore-tab-name");
            node.Attributes.Remove("data-lore-tab-slug");
        }

        return sections;
    }

    private static IReadOnlyList<MediaWikiRedirectTarget> ExtractRedirectTargets(HtmlDocument document)
    {
        var redirectNodes = document.DocumentNode.SelectNodes("//*[contains(@class,'redirectMsg')]");
        if (redirectNodes is null || redirectNodes.Count == 0)
        {
            return Array.Empty<MediaWikiRedirectTarget>();
        }

        var targets = new List<MediaWikiRedirectTarget>();
        foreach (var node in redirectNodes.ToArray())
        {
            var anchorNodes = node.SelectNodes(".//*[contains(@class,'redirectText')]//a");
            if (anchorNodes is not null)
            {
                foreach (var anchor in anchorNodes)
                {
                    var href = anchor.GetAttributeValue("href", string.Empty);
                    var text = HtmlEntity.DeEntitize(anchor.InnerText).Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        targets.Add(new MediaWikiRedirectTarget(text, href));
                    }
                }
            }

            node.Remove();
        }

        return targets;
    }
}
