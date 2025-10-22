using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.Wiki;

namespace LORE_LLM.Application.Commands.Index;

public sealed class IndexWikiCommandHandler : ICommandHandler<IndexWikiCommandOptions>
{
    private readonly IWikiIndexService _indexService;

    public IndexWikiCommandHandler(IWikiIndexService indexService)
    {
        _indexService = indexService;
    }

    public Task<Result<int>> HandleAsync(IndexWikiCommandOptions options, CancellationToken cancellationToken)
    {
        if (!options.WithVector)
        {
            return _indexService.BuildKeywordIndexAsync(options.Workspace, options.Project, options.ForceRefresh, cancellationToken);
        }

        var buildOptions = new WikiIndexBuildOptions(
            options.Workspace,
            options.Project,
            options.ForceRefresh,
            options.WithVector,
            options.QdrantEndpoint,
            options.QdrantApiKey,
            options.QdrantCollection,
            options.VectorDimension,
            options.EmbeddingSource);
        return _indexService.BuildRetrievalIndexesAsync(buildOptions, cancellationToken);
    }
}


