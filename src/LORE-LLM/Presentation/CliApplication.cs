using LORE_LLM.Application.Abstractions;
using LORE_LLM.Presentation.Commands.Augment;
using LORE_LLM.Presentation.Commands.Crawl;
using LORE_LLM.Presentation.Commands.Extract;
using LORE_LLM.Presentation.Commands.Integrate;
using LORE_LLM.Presentation.Commands.Investigate;
using LORE_LLM.Presentation.Commands.Translate;
using LORE_LLM.Presentation.Commands.Validate;
using LORE_LLM.Presentation.Commands.Cluster;
using LORE_LLM.Presentation.Commands.Index;
using System.CommandLine;

namespace LORE_LLM.Presentation;

public sealed class CliApplication : ICliApplication
{
    private readonly IServiceProvider _services;

    public CliApplication(IServiceProvider services)
    {
        _services = services;
    }

    public Task<int> RunAsync(string[] args)
    {
        var root = BuildRootCommand();
        return root.Parse(args).InvokeAsync();
    }

    private RootCommand BuildRootCommand()
    {
        var root = new RootCommand("LORE-LLM localization workflow CLI.");
        root.Add(ExtractCommandDefinition.Build(_services));
        root.Add(AugmentCommandDefinition.Build(_services));
        root.Add(TranslateCommandDefinition.Build(_services));
        root.Add(ValidateCommandDefinition.Build(_services));
        root.Add(IntegrateCommandDefinition.Build(_services));
        root.Add(InvestigateCommandDefinition.Build(_services));
        root.Add(WikiCrawlCommandDefinition.Build(_services));
        root.Add(ClusterCommandDefinition.Build(_services));
        root.Add(IndexWikiCommandDefinition.Build(_services));
        return root;
    }
}
