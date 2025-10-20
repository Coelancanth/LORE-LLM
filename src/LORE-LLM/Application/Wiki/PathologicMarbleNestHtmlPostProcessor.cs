using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;
using LORE_LLM.Application.Investigation;

namespace LORE_LLM.Application.Wiki;

public sealed class PathologicMarbleNestHtmlPostProcessor : IMediaWikiHtmlPostProcessor
{
    private static readonly HashSet<string> SupportedProjects = new(StringComparer.OrdinalIgnoreCase)
    {
        "pathologic2-marble-nest"
    };

    public string Id => MediaWikiHtmlPostProcessorIds.PathologicMarbleNest;

    public bool CanProcess(string sanitizedProjectName) => SupportedProjects.Contains(sanitizedProjectName);

    public void Process(string sanitizedProjectName, string pageTitle, HtmlDocument document)
    {
        RemoveUiArtifacts(document);
        ExpandTabberSections(document);
    }

    private static void RemoveUiArtifacts(HtmlDocument document)
    {
        RemoveNodesWithClass(document,
            "ambox",
            "infobox",
            "infoboxtable",
            "portable-infobox",
            "nomobile",
            "mobileonly",
            "mw-editsection",
            "mw-empty-elt",
            "gallery",
            "thumb",
            "wikia-gallery",
            "pi-image-collection",
            "pi-item",
            "pi-image");

        RemoveNodes(document,
            "//table[contains(@class,'infobox')]",
            "//table[contains(@class,'infoboxtable')]",
            "//table[contains(@class,'ambox')]",
            "//div[contains(@class,'portable-infobox')]",
            "//aside[contains(@class,'portable-infobox')]",
            "//div[contains(@class,'pi-item')]",
            "//figure[contains(@class,'pi-item')]",
            "//ul[contains(@class,'gallery')]",
            "//div[contains(@class,'gallery-image-wrapper')]",
            "//div[contains(@class,'fandom-gallery')]",
            "//table[contains(@class,'navbox')]",
            "//div[contains(@class,'navbox')]",
            "//table[contains(@class,'collapsible') and contains(@class,'vevent')]");
    }

    private static void RemoveNodes(HtmlDocument document, params string[] xPaths)
    {
        foreach (var path in xPaths)
        {
            var nodes = document.DocumentNode.SelectNodes(path);
            if (nodes is null)
            {
                continue;
            }

            foreach (var node in nodes.ToArray())
            {
                node.Remove();
            }
        }
    }

    private static void RemoveNodesWithClass(HtmlDocument document, params string[] classNames)
    {
        foreach (var className in classNames)
        {
            var nodes = document.DocumentNode.SelectNodes(
                $"//*[contains(concat(' ', normalize-space(@class), ' '), ' {className} ')]");

            if (nodes is null)
            {
                continue;
            }

            foreach (var node in nodes.ToArray())
            {
                node.Remove();
            }
        }
    }

    private static void ExpandTabberSections(HtmlDocument document)
    {
        var tabberNodes = document.DocumentNode.SelectNodes("//div[contains(@class,'wds-tabber')]");
        if (tabberNodes is null)
        {
            return;
        }

        foreach (var tabber in tabberNodes.ToArray())
        {
            var parent = tabber.ParentNode;
            if (parent is null)
            {
                continue;
            }

            var labelNodes = tabber.SelectNodes(".//div[contains(@class,'wds-tabs__tab-label')]");
            var labels = labelNodes?
                .Select(node => HtmlEntity.DeEntitize(node.InnerText).Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList() ?? new List<string>();

            var contentNodes = tabber.SelectNodes("./div[contains(@class,'wds-tab__content')]")?.ToArray();
            if (contentNodes is null || contentNodes.Length == 0)
            {
                tabber.Remove();
                continue;
            }

            for (var index = 0; index < contentNodes.Length; index++)
            {
                var content = contentNodes[index];
                var headingText = index < labels.Count ? labels[index] : $"Section {index + 1}";

                var headingNode = document.CreateElement("h2");
                headingNode.InnerHtml = HtmlEntity.Entitize(headingText);

                content.Attributes.RemoveAll();
                content.SetAttributeValue("data-lore-tab-name", headingText);
                content.SetAttributeValue("data-lore-tab-slug", TextSlugger.ToSlug(headingText));
                content.PrependChild(headingNode);

                parent.InsertBefore(content, tabber);
            }

            tabber.Remove();
        }
    }
}
