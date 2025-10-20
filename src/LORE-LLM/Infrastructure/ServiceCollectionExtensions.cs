using System;
using LORE_LLM.Application.Abstractions;
using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Commands.Augment;
using LORE_LLM.Application.Commands.Crawl;
using LORE_LLM.Application.Commands.Extract;
using LORE_LLM.Application.Commands.Integrate;
using LORE_LLM.Application.Commands.Investigate;
using LORE_LLM.Application.Commands.Translate;
using LORE_LLM.Application.Commands.Validate;
using LORE_LLM.Application.Extraction;
using LORE_LLM.Application.Investigation;
using LORE_LLM.Application.PostProcessing;
using LORE_LLM.Application.Wiki;
using LORE_LLM.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace LORE_LLM.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLoreLlmServices(this IServiceCollection services)
    {
        services.AddSingleton<ICliApplication, CliApplication>();
        services.AddSingleton<IProjectNameSanitizer, ProjectNameSanitizer>();
        services.AddSingleton<IRawTextExtractor, RawTextExtractor>();
        services.AddSingleton<PostProcessingPipeline>();
        services.AddSingleton<IPostExtractionProcessor, MarbleNestPostProcessor>();
        services.AddHttpClient<IMediaWikiIngestionService, MediaWikiIngestionService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            if (!client.DefaultRequestHeaders.UserAgent.TryParseAdd("LORE-LLM/0.1 (+https://lore-llm.local)"))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LORE-LLM/0.1");
            }
        });
        services.AddHttpClient<IMediaWikiCrawler, MediaWikiCrawler>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            if (!client.DefaultRequestHeaders.UserAgent.TryParseAdd("LORE-LLM/0.1 (+https://lore-llm.local)"))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LORE-LLM/0.1");
            }
        });
        services.AddSingleton<InvestigationReportGenerator>();
        services.AddSingleton<IInvestigationWorkflow, InvestigationWorkflow>();

        services.AddSingleton<ICommandHandler<ExtractCommandOptions>, ExtractCommandHandler>();
        services.AddSingleton<ICommandHandler<AugmentCommandOptions>, AugmentCommandHandler>();
        services.AddSingleton<ICommandHandler<TranslateCommandOptions>, TranslateCommandHandler>();
        services.AddSingleton<ICommandHandler<ValidateCommandOptions>, ValidateCommandHandler>();
        services.AddSingleton<ICommandHandler<IntegrateCommandOptions>, IntegrateCommandHandler>();
        services.AddSingleton<ICommandHandler<InvestigationCommandOptions>, InvestigationCommandHandler>();
        services.AddSingleton<ICommandHandler<WikiCrawlCommandOptions>, WikiCrawlCommandHandler>();

        return services;
    }
}
