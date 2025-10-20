using System;
using System.CommandLine;
using System.IO;
using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Commands.Crawl;
using Microsoft.Extensions.DependencyInjection;

namespace LORE_LLM.Presentation.Commands.Crawl;

internal static class WikiCrawlCommandDefinition
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
            Description = "Re-fetch wiki content even if markdown files already exist."
        };

        var pageOption = new Option<string[]>("--page")
        {
            Description = "Specific wiki page titles to crawl (can be supplied multiple times)."
        };

        var maxPagesOption = new Option<int>("--max-pages")
        {
            Description = "Limit the number of pages to crawl (0 = no limit)."
        };

        var command = new Command("crawl-wiki", "Download wiki pages as markdown files via the MediaWiki API.");
        command.Options.Add(workspaceOption);
        command.Options.Add(projectOption);
        command.Options.Add(forceRefreshOption);
        command.Options.Add(pageOption);
        command.Options.Add(maxPagesOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var workspace = parseResult.GetValue(workspaceOption)!;
            var project = parseResult.GetValue(projectOption) ?? "default";
            var force = parseResult.GetValue(forceRefreshOption);
            var pages = parseResult.GetValue(pageOption);
            var maxPages = parseResult.GetValue(maxPagesOption);

            var handler = services.GetRequiredService<ICommandHandler<WikiCrawlCommandOptions>>();
            var result = await handler.HandleAsync(
                new WikiCrawlCommandOptions(workspace, project, force, pages, maxPages),
                cancellationToken);

            if (result.IsSuccess)
            {
                Console.WriteLine($"Crawled {result.Value} pages.");
                return 0;
            }

            Console.Error.WriteLine(result.Error);
            return 1;
        });

        return command;
    }
}








