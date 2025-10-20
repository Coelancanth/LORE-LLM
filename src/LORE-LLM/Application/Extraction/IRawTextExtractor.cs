using CSharpFunctionalExtensions;

namespace LORE_LLM.Application.Extraction;

public interface IRawTextExtractor
{
    Task<Result<int>> ExtractAsync(FileInfo inputFile, DirectoryInfo workspace, CancellationToken cancellationToken);
}
