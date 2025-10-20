using System;
using System.CommandLine;
using System.IO;
using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Commands.Cluster;
using Microsoft.Extensions.DependencyInjection;

namespace LORE_LLM.Presentation.Commands.Cluster;

internal static class ClusterCommandDefinition
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

        var providerOption = new Option<string>("--provider")
        {
            Description = "Chat provider name (e.g., local, cursor, openai, claude).",
            Required = false
        };

        var batchSizeOption = new Option<int>("--batch-size")
        {
            Description = "Number of segments per batch.",
            Required = false
        };

        var includeEmptyOption = new Option<bool>("--include-empty")
        {
            Description = "Include empty segments in clustering."
        };

        var promptTemplateOption = new Option<FileInfo?>("--prompt-template")
        {
            Description = "Path to a custom prompt template file.",
            Required = false
        };

        var saveTranscriptOption = new Option<bool>("--save-transcript")
        {
            Description = "Save prompt/response transcript to markdown."
        };

        var maxSegmentsOption = new Option<int>("--max-segments")
        {
            Description = "Upper limit on number of segments to process (testing/throttling).",
            Required = false
        };

        var command = new Command("cluster", "Cluster segments via LLM with a pluggable chat provider.");
        command.Options.Add(workspaceOption);
        command.Options.Add(projectOption);
        command.Options.Add(providerOption);
        command.Options.Add(batchSizeOption);
        command.Options.Add(includeEmptyOption);
        command.Options.Add(promptTemplateOption);
        command.Options.Add(saveTranscriptOption);
        command.Options.Add(maxSegmentsOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var workspace = parseResult.GetValue(workspaceOption)!;
            var project = parseResult.GetValue(projectOption) ?? "default";
            var provider = parseResult.GetValue(providerOption) ?? "local";
            var batchSize = parseResult.GetValue(batchSizeOption);
            var includeEmpty = parseResult.GetValue(includeEmptyOption);
            var promptTemplate = parseResult.GetValue(promptTemplateOption);
            var saveTranscript = parseResult.GetValue(saveTranscriptOption);
            var maxSegments = parseResult.GetValue(maxSegmentsOption);

            var handler = services.GetRequiredService<ICommandHandler<ClusterCommandOptions>>();
            var result = await handler.HandleAsync(
                new ClusterCommandOptions(workspace, project, provider, batchSize == 0 ? 25 : batchSize, includeEmpty, promptTemplate, saveTranscript) with { MaxSegments = maxSegments },
                cancellationToken);

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


