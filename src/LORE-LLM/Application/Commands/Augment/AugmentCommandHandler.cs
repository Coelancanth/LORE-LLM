using CSharpFunctionalExtensions;

namespace LORE_LLM.Application.Commands.Augment;

public sealed class AugmentCommandHandler : ICommandHandler<AugmentCommandOptions>
{
    public Task<Result<int>> HandleAsync(AugmentCommandOptions options, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[augment] workspace={options.Workspace.FullName}");
        return Task.FromResult(Result.Success(0));
    }
}
