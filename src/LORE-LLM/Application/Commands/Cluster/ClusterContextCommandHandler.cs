using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.Clustering;

namespace LORE_LLM.Application.Commands.Cluster;

public sealed class ClusterContextCommandHandler : ICommandHandler<ClusterContextCommandOptions>
{
	private readonly IClusterContextWorkflow _workflow;

	public ClusterContextCommandHandler(IClusterContextWorkflow workflow)
	{
		_workflow = workflow;
	}

	public Task<Result<int>> HandleAsync(ClusterContextCommandOptions options, CancellationToken cancellationToken)
	{
		return _workflow.RunAsync(options, cancellationToken);
	}
}


