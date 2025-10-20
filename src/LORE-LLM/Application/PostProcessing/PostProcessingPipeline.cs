using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;

namespace LORE_LLM.Application.PostProcessing;

public sealed class PostProcessingPipeline
{
    private readonly IReadOnlyCollection<IPostExtractionProcessor> _processors;

    public PostProcessingPipeline(IEnumerable<IPostExtractionProcessor> processors)
    {
        _processors = processors.ToList();
    }

    public async Task<Result> RunAsync(PostProcessingContext context, CancellationToken cancellationToken)
    {
        foreach (var processor in _processors)
        {
            if (!processor.CanProcess(context.SanitizedProjectName))
            {
                continue;
            }

            var result = await processor.ProcessAsync(context, cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }
}
