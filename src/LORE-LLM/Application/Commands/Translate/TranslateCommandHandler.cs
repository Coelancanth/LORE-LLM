using CSharpFunctionalExtensions;

namespace LORE_LLM.Application.Commands.Translate;

public sealed class TranslateCommandHandler : ICommandHandler<TranslateCommandOptions>
{
    public Task<Result<int>> HandleAsync(TranslateCommandOptions options, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[translate] workspace={options.Workspace.FullName} language={options.TargetLanguage}");
        return Task.FromResult(Result.Success(0));
    }
}
