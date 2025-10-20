using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Commands.Integrate;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace LORE_LLM.Presentation.Commands.Integrate;

internal static class IntegrateCommandDefinition
{
    public static Command Build(IServiceProvider services)
    {
        var workspaceOption = new Option<DirectoryInfo>("--workspace", "-w")
        {
            Description = "Workspace directory containing approved translations.",
            Required = true
        };

        var destinationOption = new Option<DirectoryInfo>("--destination", "-d")
        {
            Description = "Output directory for game-ready localization files.",
            Required = true
        };

        var command = new Command("integrate", "Integrate approved translations into the target game format.");
        command.Options.Add(workspaceOption);
        command.Options.Add(destinationOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var workspace = parseResult.GetValue(workspaceOption)!;
            var destination = parseResult.GetValue(destinationOption)!;

            var handler = services.GetRequiredService<ICommandHandler<IntegrateCommandOptions>>();
            var result = await handler.HandleAsync(new IntegrateCommandOptions(workspace, destination), cancellationToken);

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
