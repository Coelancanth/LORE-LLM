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

        var withVectorOption = new Option<bool>("--with-vector")
        {
            Description = "Also build/register a vector retrieval provider (Qdrant)."
        };

        var qdrantEndpointOption = new Option<string>("--qdrant-endpoint")
        {
            Description = "Qdrant HTTP endpoint (e.g., http://localhost:6333)."
        };

        var qdrantApiKeyOption = new Option<string?>("--qdrant-api-key")
        {
            Description = "Optional Qdrant API key (also read from QDRANT_API_KEY)."
        };

        var qdrantCollectionOption = new Option<string>("--qdrant-collection")
        {
            Description = "Target Qdrant collection name."
        };

        var vectorDimensionOption = new Option<int>("--vector-dimension")
        {
            Description = "Embedding vector dimension."
        };

        var embeddingSourceOption = new Option<string>("--embedding-source")
        {
            Description = "Embedding provider hint (e.g., 'none', 'bge-small', 'e5-small')."
        };

        var command = new Command("index-wiki", "Create or refresh retrieval indexes (keyword, optional vector) from crawled markdown.");
        command.Options.Add(workspaceOption);
        command.Options.Add(projectOption);
        command.Options.Add(forceRefreshOption);
        command.Options.Add(withVectorOption);
        command.Options.Add(qdrantEndpointOption);
        command.Options.Add(qdrantApiKeyOption);
        command.Options.Add(qdrantCollectionOption);
        command.Options.Add(vectorDimensionOption);
        command.Options.Add(embeddingSourceOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var workspace = parseResult.GetValue(workspaceOption)!;
            var project = parseResult.GetValue(projectOption) ?? "default";
            var force = parseResult.GetValue(forceRefreshOption);
            var withVector = parseResult.GetValue(withVectorOption);
            var qdrantEndpoint = parseResult.GetValue(qdrantEndpointOption) ?? "http://localhost:6333";
            var qdrantApiKey = parseResult.GetValue(qdrantApiKeyOption) ?? Environment.GetEnvironmentVariable("QDRANT_API_KEY");
            var qdrantCollection = parseResult.GetValue(qdrantCollectionOption) ?? "lore_llm_wiki";
            var vectorDimension = parseResult.GetValue(vectorDimensionOption);
            if (vectorDimension <= 0) vectorDimension = 384;
            var embeddingSource = parseResult.GetValue(embeddingSourceOption) ?? "none";

            var handler = services.GetRequiredService<ICommandHandler<IndexWikiCommandOptions>>();
            var result = await handler.HandleAsync(new IndexWikiCommandOptions(
                workspace,
                project,
                force,
                withVector,
                qdrantEndpoint,
                qdrantApiKey,
                qdrantCollection,
                vectorDimension,
                embeddingSource), cancellationToken);

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


