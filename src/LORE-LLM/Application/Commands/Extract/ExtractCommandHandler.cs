using CSharpFunctionalExtensions;
using LORE_LLM.Application.Extraction;

namespace LORE_LLM.Application.Commands.Extract;

public sealed class ExtractCommandHandler : ICommandHandler<ExtractCommandOptions>
{
    private readonly IRawTextExtractor _extractor;

    public ExtractCommandHandler(IRawTextExtractor extractor)
    {
        _extractor = extractor;
    }

    public Task<Result<int>> HandleAsync(ExtractCommandOptions options, CancellationToken cancellationToken)
    {
        return _extractor.ExtractAsync(options.Input, options.Output, options.Project, cancellationToken);
    }
}
