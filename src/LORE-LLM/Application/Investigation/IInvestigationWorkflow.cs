using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.Commands.Investigate;

namespace LORE_LLM.Application.Investigation;

public interface IInvestigationWorkflow
{
    Task<Result<int>> RunAsync(InvestigationCommandOptions options, CancellationToken cancellationToken);
}
