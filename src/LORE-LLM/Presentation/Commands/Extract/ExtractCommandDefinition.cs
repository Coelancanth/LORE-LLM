using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Commands.Extract;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace LORE_LLM.Presentation.Commands.Extract;

internal static class ExtractCommandDefinition
{
    public static Command Build(IServiceProvider services)
    {
        var inputOption = new Option<FileInfo>("--input", "-i")
        {
            Description = "Path to the raw input text file.",
            Required = true
        };

        var outputOption = new Option<DirectoryInfo>("--output", "-o")
        {
            Description = "Directory where extraction artifacts will be written.",
            Required = true
        };

        var projectOption = new Option<string>("--project", "-p")
        {
            Description = "Project identifier used to classify extraction output.",
            Required = false
        };

        var skipPostProcessOption = new Option<bool>("--skip-post-process", "--no-post-process")
        {
            Description = "Skip post-processing of extracted artifacts."
        };

        var command = new Command("extract", "Extract raw text and produce source artifacts ready for the pipeline.");
        command.Options.Add(inputOption);
        command.Options.Add(outputOption);
        command.Options.Add(projectOption);
        command.Options.Add(skipPostProcessOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var input = parseResult.GetValue(inputOption)!;
            var output = parseResult.GetValue(outputOption)!;
            var project = parseResult.GetValue(projectOption) ?? "default";
            var skipPostProcessing = parseResult.GetValue(skipPostProcessOption);

            var handler = services.GetRequiredService<ICommandHandler<ExtractCommandOptions>>();
            var result = await handler.HandleAsync(new ExtractCommandOptions(input, output, project, !skipPostProcessing), cancellationToken);

            if (result.IsSuccess)
            {
                return result.Value;
            }

            Console.Error.WriteLine(result.Error);
            return 1;
        });

        return command;
    }
}
