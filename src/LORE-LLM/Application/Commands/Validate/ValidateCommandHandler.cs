using CSharpFunctionalExtensions;

namespace LORE_LLM.Application.Commands.Validate;

public sealed class ValidateCommandHandler : ICommandHandler<ValidateCommandOptions>
{
    public Task<Result<int>> HandleAsync(ValidateCommandOptions options, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[validate] workspace={options.Workspace.FullName}");
        return Task.FromResult(Result.Success(0));
    }
}
