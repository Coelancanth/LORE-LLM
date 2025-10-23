using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.Clustering;

namespace LORE_LLM.Application.Commands.Cluster;

public sealed class ClusterPrepCommandHandler : ICommandHandler<ClusterPrepCommandOptions>
{
    private readonly IClusterPrepWorkflow _workflow;

    public ClusterPrepCommandHandler(IClusterPrepWorkflow workflow)
    {
        _workflow = workflow;
    }

    public Task<Result<int>> HandleAsync(ClusterPrepCommandOptions options, CancellationToken cancellationToken)
    {
        return _workflow.RunAsync(options, cancellationToken);
    }
}


