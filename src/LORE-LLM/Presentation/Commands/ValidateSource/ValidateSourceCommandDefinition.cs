using System.CommandLine;
using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Commands.ValidateSource;
using Microsoft.Extensions.DependencyInjection;

namespace LORE_LLM.Presentation.Commands.ValidateSource;

internal static class ValidateSourceCommandDefinition
{
    public static Command Build(IServiceProvider services)
    {
        var workspaceOption = new Option<DirectoryInfo>("--workspace", "-w")
        {
            Description = "Workspace directory containing project folder(s).",
            Required = true
        };
        var projectOption = new Option<string?>("--project", "-p")
        {
            Description = "Project display name used during extraction.",
            Required = false
        };

        var command = new Command("validate-source", "Validate source_text_raw.json against the canonical contract.");
        command.Options.Add(workspaceOption);
        command.Options.Add(projectOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var workspace = parseResult.GetValue(workspaceOption)!;
            var project = parseResult.GetValue(projectOption);

            var handler = services.GetRequiredService<ICommandHandler<ValidateSourceCommandOptions>>();
            var result = await handler.HandleAsync(new ValidateSourceCommandOptions(workspace, project), cancellationToken);

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


