using CSharpFunctionalExtensions;

namespace LORE_LLM.Application.Commands.Extract;

public sealed class ExtractCommandHandler : ICommandHandler<ExtractCommandOptions>
{
    public Task<Result<int>> HandleAsync(ExtractCommandOptions options, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[extract] input={options.Input.FullName} output={options.Output.FullName}");
        return Task.FromResult(Result.Success(0));
    }
}
