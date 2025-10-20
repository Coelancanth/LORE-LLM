using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;

namespace LORE_LLM.Application.Wiki;

public interface IWikiIndexService
{
    Task<Result<int>> BuildKeywordIndexAsync(DirectoryInfo workspace, string projectDisplayName, bool forceRefresh, CancellationToken cancellationToken);
}


