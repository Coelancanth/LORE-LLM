using System;
using System.IO;
using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Commands.Investigate;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace LORE_LLM.Presentation.Commands.Investigate;

internal static class InvestigateCommandDefinition
{
    public static Command Build(IServiceProvider services)
    {
        var workspaceOption = new Option<DirectoryInfo>("--workspace", "-w")
        {
            Description = "Workspace directory containing project artifacts.",
            Required = true
        };

        var projectOption = new Option<string>("--project", "-p")
        {
            Description = "Project display name used during extraction.",
            Required = false
        };

        var forceRefreshOption = new Option<bool>("--force-refresh")
        {
            Description = "Re-fetch wiki knowledge even if it is already cached."
        };

        var offlineOption = new Option<bool>("--offline")
        {
            Description = "Skip network calls and use only cached artifacts."
        };

        var command = new Command("investigate", "Analyze extracted segments and produce investigation report artifacts.");
        command.Options.Add(workspaceOption);
        command.Options.Add(projectOption);
        command.Options.Add(forceRefreshOption);
        command.Options.Add(offlineOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var workspace = parseResult.GetValue(workspaceOption)!;
            var project = parseResult.GetValue(projectOption) ?? "default";
            var forceRefresh = parseResult.GetValue(forceRefreshOption);
            var offline = parseResult.GetValue(offlineOption);

            var handler = services.GetRequiredService<ICommandHandler<InvestigationCommandOptions>>();
            var result = await handler.HandleAsync(new InvestigationCommandOptions(workspace, project, forceRefresh, offline), cancellationToken);

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
