using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Commands.Validate;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace LORE_LLM.Presentation.Commands.Validate;

internal static class ValidateCommandDefinition
{
    public static Command Build(IServiceProvider services)
    {
        var workspaceOption = new Option<DirectoryInfo>("--workspace", "-w")
        {
            Description = "Workspace directory containing translation artifacts.",
            Required = true
        };

        var command = new Command("validate", "Run QA checks against generated artifacts.");
        command.Options.Add(workspaceOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var workspace = parseResult.GetValue(workspaceOption)!;

            var handler = services.GetRequiredService<ICommandHandler<ValidateCommandOptions>>();
            var result = await handler.HandleAsync(new ValidateCommandOptions(workspace), cancellationToken);

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
