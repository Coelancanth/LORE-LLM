using CSharpFunctionalExtensions;

namespace LORE_LLM.Application.Validation;

public interface ISourceSchemaValidator
{
    Task<Result> ValidateAsync(DirectoryInfo workspace, string? project, CancellationToken cancellationToken);
}


