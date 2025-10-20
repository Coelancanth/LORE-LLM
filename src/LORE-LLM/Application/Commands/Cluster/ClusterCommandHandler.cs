using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.Clustering;

namespace LORE_LLM.Application.Commands.Cluster;

public sealed class ClusterCommandHandler : ICommandHandler<ClusterCommandOptions>
{
    private readonly IClusterWorkflow _workflow;

    public ClusterCommandHandler(IClusterWorkflow workflow)
    {
        _workflow = workflow;
    }

    public Task<Result<int>> HandleAsync(ClusterCommandOptions options, CancellationToken cancellationToken)
    {
        return _workflow.RunAsync(options, cancellationToken);
    }
}


