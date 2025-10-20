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

        var command = new Command("extract", "Extract raw text and produce source artifacts ready for the pipeline.");
        command.Options.Add(inputOption);
        command.Options.Add(outputOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var input = parseResult.GetValue(inputOption)!;
            var output = parseResult.GetValue(outputOption)!;

            var handler = services.GetRequiredService<ICommandHandler<ExtractCommandOptions>>();
            var result = await handler.HandleAsync(new ExtractCommandOptions(input, output), cancellationToken);

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
