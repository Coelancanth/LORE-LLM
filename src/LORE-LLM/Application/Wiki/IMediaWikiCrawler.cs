using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;

namespace LORE_LLM.Application.Wiki;

public interface IMediaWikiCrawler
{
    Task<Result<int>> CrawlAsync(
        DirectoryInfo workspace,
        string projectDisplayName,
        bool forceRefresh,
        string[]? specificPages,
        int maxPages,
        CancellationToken cancellationToken);
}
