using CSharpFunctionalExtensions;

namespace LORE_LLM.Application.Commands;

public interface ICommandHandler<in TOptions>
{
    Task<Result<int>> HandleAsync(TOptions options, CancellationToken cancellationToken);
}
