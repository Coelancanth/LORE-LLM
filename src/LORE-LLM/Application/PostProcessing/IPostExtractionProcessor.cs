using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;

namespace LORE_LLM.Application.PostProcessing;

public interface IPostExtractionProcessor
{
    bool CanProcess(string sanitizedProject);

    Task<Result> ProcessAsync(PostProcessingContext context, CancellationToken cancellationToken);
}
