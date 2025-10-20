using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Commands.Augment;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace LORE_LLM.Presentation.Commands.Augment;

internal static class AugmentCommandDefinition
{
    public static Command Build(IServiceProvider services)
    {
        var workspaceOption = new Option<DirectoryInfo>("--workspace", "-w")
        {
            Description = "Workspace directory containing extracted artifacts.",
            Required = true
        };

        var command = new Command("augment", "Augment segments with inferred metadata and knowledge base hints.");
        command.Options.Add(workspaceOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var workspace = parseResult.GetValue(workspaceOption)!;

            var handler = services.GetRequiredService<ICommandHandler<AugmentCommandOptions>>();
            var result = await handler.HandleAsync(new AugmentCommandOptions(workspace), cancellationToken);

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
