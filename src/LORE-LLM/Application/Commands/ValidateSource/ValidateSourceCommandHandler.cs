using CSharpFunctionalExtensions;
using LORE_LLM.Application.Validation;

namespace LORE_LLM.Application.Commands.ValidateSource;

public sealed class ValidateSourceCommandHandler : ICommandHandler<ValidateSourceCommandOptions>
{
    private readonly ISourceSchemaValidator _validator;

    public ValidateSourceCommandHandler(ISourceSchemaValidator validator)
    {
        _validator = validator;
    }

    public async Task<Result<int>> HandleAsync(ValidateSourceCommandOptions options, CancellationToken cancellationToken)
    {
        var result = await _validator.ValidateAsync(options.Workspace, options.Project, cancellationToken);
        if (result.IsFailure)
        {
            Console.Error.WriteLine(result.Error);
            return Result.Failure<int>(result.Error);
        }

        Console.WriteLine("source_text_raw.json is valid.");
        return Result.Success(0);
    }
}


