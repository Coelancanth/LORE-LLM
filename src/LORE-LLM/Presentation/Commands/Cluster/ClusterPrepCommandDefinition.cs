using System;
using System.CommandLine;
using System.IO;
using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Commands.Cluster;
using Microsoft.Extensions.DependencyInjection;

namespace LORE_LLM.Presentation.Commands.Cluster;

internal static class ClusterPrepCommandDefinition
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
        var includeEmptyOption = new Option<bool>("--include-empty")
        {
            Description = "Include empty segments in pre-clustering."
        };

        var cmd = new Command("cluster-prep", "Deterministically bucket segments and write clusters_precomputed.json.");
        cmd.Options.Add(workspaceOption);
        cmd.Options.Add(projectOption);
        cmd.Options.Add(includeEmptyOption);

        cmd.SetAction(async (parseResult, cancellationToken) =>
        {
            var workspace = parseResult.GetValue(workspaceOption)!;
            var project = parseResult.GetValue(projectOption) ?? "default";
            var includeEmpty = parseResult.GetValue(includeEmptyOption);
            var handler = services.GetRequiredService<ICommandHandler<ClusterPrepCommandOptions>>();
            var result = await handler.HandleAsync(new ClusterPrepCommandOptions(workspace, project, includeEmpty), cancellationToken);
            if (result.IsSuccess)
            {
                Console.WriteLine($"Wrote clusters_precomputed.json with {result.Value} clusters.");
                return 0;
            }
            Console.Error.WriteLine(result.Error);
            return 1;
        });

        return cmd;
    }
}


