using HtmlAgilityPack;

namespace LORE_LLM.Application.Wiki;

public interface IMediaWikiHtmlPostProcessor
{
    string Id { get; }

    bool CanProcess(string sanitizedProjectName);

    void Process(string sanitizedProjectName, string pageTitle, HtmlDocument document);
}
