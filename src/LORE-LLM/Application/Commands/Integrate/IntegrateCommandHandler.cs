using CSharpFunctionalExtensions;

namespace LORE_LLM.Application.Commands.Integrate;

public sealed class IntegrateCommandHandler : ICommandHandler<IntegrateCommandOptions>
{
    public Task<Result<int>> HandleAsync(IntegrateCommandOptions options, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[integrate] workspace={options.Workspace.FullName} destination={options.Destination.FullName}");
        return Task.FromResult(Result.Success(0));
    }
}
