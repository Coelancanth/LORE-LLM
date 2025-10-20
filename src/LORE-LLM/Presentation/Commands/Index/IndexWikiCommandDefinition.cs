using System;
using System.CommandLine;
using System.IO;
using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Commands.Index;
using Microsoft.Extensions.DependencyInjection;

namespace LORE_LLM.Presentation.Commands.Index;

internal static class IndexWikiCommandDefinition
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
            Description = "Rebuild the keyword index even if it already exists."
        };

        var command = new Command("index-wiki", "Create or refresh the wiki keyword index from crawled markdown.");
        command.Options.Add(workspaceOption);
        command.Options.Add(projectOption);
        command.Options.Add(forceRefreshOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var workspace = parseResult.GetValue(workspaceOption)!;
            var project = parseResult.GetValue(projectOption) ?? "default";
            var force = parseResult.GetValue(forceRefreshOption);

            var handler = services.GetRequiredService<ICommandHandler<IndexWikiCommandOptions>>();
            var result = await handler.HandleAsync(new IndexWikiCommandOptions(workspace, project, force), cancellationToken);

            if (result.IsSuccess)
            {
                Console.WriteLine($"Indexed {result.Value} wiki entries.");
                return 0;
            }

            Console.Error.WriteLine(result.Error);
            return 1;
        });

        return command;
    }
}


