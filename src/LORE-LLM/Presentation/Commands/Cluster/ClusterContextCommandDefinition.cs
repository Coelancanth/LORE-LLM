using System;
using System.CommandLine;
using System.IO;
using LORE_LLM.Application.Clustering;
using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Commands.Cluster;
using Microsoft.Extensions.DependencyInjection;

namespace LORE_LLM.Presentation.Commands.Cluster;

internal static class ClusterContextCommandDefinition
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
            Description = "Project display name used during extraction."
        };
        var topKOption = new Option<int>("--top-k")
        {
            Description = "Max results per cluster."
        };

        var cmd = new Command("cluster-context", "Generate cluster_context.json by querying retrieval providers.");
        cmd.Options.Add(workspaceOption);
        cmd.Options.Add(projectOption);
        cmd.Options.Add(topKOption);

		cmd.SetAction(async (parseResult, cancellationToken) =>
		{
			var workspace = parseResult.GetValue(workspaceOption)!;
			var project = parseResult.GetValue(projectOption)!;
			var topK = parseResult.GetValue(topKOption);
			var handler = services.GetRequiredService<ICommandHandler<ClusterContextCommandOptions>>();
			var result = await handler.HandleAsync(new ClusterContextCommandOptions(workspace, project, topK), cancellationToken);
			if (result.IsSuccess)
			{
				Console.WriteLine($"Wrote cluster_context.json for {result.Value} clusters.");
				return 0;
			}
			Console.Error.WriteLine(result.Error);
			return 1;
		});

		return cmd;
	}
}


