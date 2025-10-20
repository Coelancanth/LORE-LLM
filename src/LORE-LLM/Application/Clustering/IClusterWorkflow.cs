using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.Commands.Cluster;

namespace LORE_LLM.Application.Clustering;

public interface IClusterWorkflow
{
    Task<Result<int>> RunAsync(ClusterCommandOptions options, CancellationToken cancellationToken);
}


