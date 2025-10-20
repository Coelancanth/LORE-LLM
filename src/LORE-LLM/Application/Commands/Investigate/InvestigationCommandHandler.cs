using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Investigation;

namespace LORE_LLM.Application.Commands.Investigate;

public sealed class InvestigationCommandHandler : ICommandHandler<InvestigationCommandOptions>
{
    private readonly IInvestigationWorkflow _workflow;

    public InvestigationCommandHandler(IInvestigationWorkflow workflow)
    {
        _workflow = workflow;
    }

    public Task<Result<int>> HandleAsync(InvestigationCommandOptions options, CancellationToken cancellationToken)
    {
        return _workflow.RunAsync(options, cancellationToken);
    }
}
