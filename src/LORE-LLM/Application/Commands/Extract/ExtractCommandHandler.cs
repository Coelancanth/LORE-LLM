using CSharpFunctionalExtensions;
using LORE_LLM.Application.Extraction;
using LORE_LLM.Application.PostProcessing;

namespace LORE_LLM.Application.Commands.Extract;

public sealed class ExtractCommandHandler : ICommandHandler<ExtractCommandOptions>
{
    private readonly IRawTextExtractor _extractor;
    private readonly PostProcessingPipeline _postProcessingPipeline;
    private readonly IProjectNameSanitizer _projectNameSanitizer;

    public ExtractCommandHandler(
        IRawTextExtractor extractor,
        PostProcessingPipeline postProcessingPipeline,
        IProjectNameSanitizer projectNameSanitizer)
    {
        _extractor = extractor;
        _postProcessingPipeline = postProcessingPipeline;
        _projectNameSanitizer = projectNameSanitizer;
    }

    public async Task<Result<int>> HandleAsync(ExtractCommandOptions options, CancellationToken cancellationToken)
    {
        var extractResult = await _extractor.ExtractAsync(options.Input, options.Output, options.Project, cancellationToken);
        if (extractResult.IsFailure)
        {
            return extractResult;
        }

        if (!options.RunPostProcessing)
        {
            return extractResult;
        }

        var sanitizedProject = _projectNameSanitizer.Sanitize(options.Project);
        var projectDirectory = new DirectoryInfo(Path.Combine(options.Output.FullName, sanitizedProject));

        var postProcessingResult = await _postProcessingPipeline.RunAsync(
            new PostProcessingContext(
                options.Project,
                sanitizedProject,
                projectDirectory),
            cancellationToken);

        return postProcessingResult.IsSuccess ? extractResult : Result.Failure<int>(postProcessingResult.Error);
    }
}
