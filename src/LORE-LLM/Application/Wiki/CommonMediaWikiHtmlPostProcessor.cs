using System.Linq;
using HtmlAgilityPack;

namespace LORE_LLM.Application.Wiki;

public sealed class CommonMediaWikiHtmlPostProcessor : IMediaWikiHtmlPostProcessor
{
    public string Id => MediaWikiHtmlPostProcessorIds.Common;

    public bool CanProcess(string sanitizedProjectName) => true;

    public void Process(string sanitizedProjectName, string pageTitle, HtmlDocument document)
    {
        RemoveCommentNodes(document);
        RemoveNodes(document,
            "//script",
            "//style",
            "//noscript",
            "//link",
            "//meta",
            "//iframe",
            "//figure",
            "//img",
            "//div[contains(@class,'mw-editsection')]");
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

    private static void RemoveCommentNodes(HtmlDocument document)
    {
        var comments = document.DocumentNode.SelectNodes("//comment()");
        if (comments is null)
        {
            return;
        }

        foreach (var comment in comments.ToArray())
        {
            comment.Remove();
        }
    }
}
