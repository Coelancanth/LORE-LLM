using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.Wiki;

namespace LORE_LLM.Application.Commands.Crawl;

public sealed class WikiCrawlCommandHandler : ICommandHandler<WikiCrawlCommandOptions>
{
    private readonly IMediaWikiCrawler _crawler;

    public WikiCrawlCommandHandler(IMediaWikiCrawler crawler)
    {
        _crawler = crawler;
    }

    public Task<Result<int>> HandleAsync(WikiCrawlCommandOptions options, CancellationToken cancellationToken)
    {
        return _crawler.CrawlAsync(
            options.Workspace,
            options.Project,
            options.ForceRefresh,
            options.Pages,
            options.MaxPages,
            cancellationToken);
    }
}
