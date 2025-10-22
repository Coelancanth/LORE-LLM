using System.CommandLine;
using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Commands.EnrichMetadata;
using Microsoft.Extensions.DependencyInjection;

namespace LORE_LLM.Presentation.Commands.EnrichMetadata;

internal static class EnrichMetadataCommandDefinition
{
    public static Command Build(IServiceProvider services)
    {
        var workspaceOption = new Option<DirectoryInfo>("--workspace", "-w")
        {
            Description = "Workspace directory containing project folder(s).",
            Required = true
        };
        var projectOption = new Option<string>("--project", "-p")
        {
            Description = "Project display name used during extraction.",
            Required = true
        };
        var configOption = new Option<FileInfo?>("--config")
        {
            Description = "Optional path to a specific enrichment config JSON file.",
            Required = false
        };

        var command = new Command("enrich-metadata", "Deterministically enrich source segment metadata using layered configs.");
        command.Options.Add(workspaceOption);
        command.Options.Add(projectOption);
        command.Options.Add(configOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var workspace = parseResult.GetValue(workspaceOption)!;
            var project = parseResult.GetValue(projectOption)!;
            var config = parseResult.GetValue(configOption);

            var handler = services.GetRequiredService<ICommandHandler<EnrichMetadataCommandOptions>>();
            var result = await handler.HandleAsync(new EnrichMetadataCommandOptions(workspace, project, config), cancellationToken);

            if (result.IsSuccess) return result.Value;
            Console.Error.WriteLine(result.Error);
            return 1;
        });

        return command;
    }
}


