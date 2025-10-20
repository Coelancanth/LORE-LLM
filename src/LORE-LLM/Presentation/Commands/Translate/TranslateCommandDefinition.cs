using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Commands.Translate;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace LORE_LLM.Presentation.Commands.Translate;

internal static class TranslateCommandDefinition
{
    public static Command Build(IServiceProvider services)
    {
        var workspaceOption = new Option<DirectoryInfo>("--workspace", "-w")
        {
            Description = "Workspace directory containing augmented artifacts.",
            Required = true
        };

        var languageOption = new Option<string>("--language", "-l")
        {
            Description = "Target language code (e.g., 'ru', 'es').",
            Required = true
        };

        var command = new Command("translate", "Translate content using configured LLM providers.");
        command.Options.Add(workspaceOption);
        command.Options.Add(languageOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var workspace = parseResult.GetValue(workspaceOption)!;
            var language = parseResult.GetValue(languageOption)!;

            var handler = services.GetRequiredService<ICommandHandler<TranslateCommandOptions>>();
            var result = await handler.HandleAsync(new TranslateCommandOptions(workspace, language), cancellationToken);

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
