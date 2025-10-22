using System;
using LORE_LLM.Application.Abstractions;
using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Commands.Augment;
using LORE_LLM.Application.Commands.Crawl;
using LORE_LLM.Application.Commands.Cluster;
using LORE_LLM.Application.Commands.Extract;
using LORE_LLM.Application.Commands.Integrate;
using LORE_LLM.Application.Commands.Investigate;
using LORE_LLM.Application.Commands.Translate;
using LORE_LLM.Application.Commands.Validate;
using LORE_LLM.Application.Extraction;
using LORE_LLM.Application.Investigation;
using LORE_LLM.Application.Clustering;
using LORE_LLM.Application.Chat;
using LORE_LLM.Application.Commands.Index;
using LORE_LLM.Application.Wiki;
using LORE_LLM.Application.PostProcessing;
using LORE_LLM.Presentation;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.IO;
using LORE_LLM.Application.Retrieval;

namespace LORE_LLM.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLoreLlmServices(this IServiceCollection services)
    {
        services.AddSingleton<ICliApplication, CliApplication>();
        services.AddSingleton<IProjectNameSanitizer, ProjectNameSanitizer>();
        services.AddSingleton<IRawTextExtractor, RawTextExtractor>();
        services.AddSingleton<PostProcessingPipeline>();
        services.AddSingleton<MediaWikiHtmlPostProcessingPipeline>();
        services.AddSingleton<IMediaWikiHtmlPostProcessor, CommonMediaWikiHtmlPostProcessor>();
        services.AddSingleton<IMediaWikiHtmlPostProcessor, PathologicMarbleNestHtmlPostProcessor>();
        services.Configure<MediaWikiCrawlerOptions>(options =>
        {
            var projectOptions = new MediaWikiCrawlerProjectOptions
            {
                ApiBase = "https://pathologic.fandom.com/api.php",
                EmitBaseDocument = false
            };
            projectOptions.HtmlPostProcessors.Add(MediaWikiHtmlPostProcessorIds.Common);
            projectOptions.HtmlPostProcessors.Add(MediaWikiHtmlPostProcessorIds.PathologicMarbleNest);
            projectOptions.TabOutputs.Add(new MediaWikiTabOutputOptions
            {
                TabName = "Pathologic 2",
                TabSlug = "pathologic-2",
                FileSuffix = "-pathologic-2",
                TitleFormat = "{title} (Pathologic 2)"
            });
            projectOptions.TabOutputs.Add(new MediaWikiTabOutputOptions
            {
                TabName = "Pathologic",
                TabSlug = "pathologic",
                FileSuffix = "-pathologic",
                TitleFormat = "{title} (Pathologic)"
            });
            options.Projects["pathologic2-marble-nest"] = projectOptions;
        });
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
        services.AddSingleton<IClusterWorkflow, ClusterWorkflow>();
        services.AddSingleton<ChatProviderResolver>();
        services.AddSingleton<IChatProvider, LocalChatProvider>();

        // Load chat providers configuration (optional)
        var chatConfig = LoadChatProvidersConfiguration();
        services.AddSingleton(chatConfig);

        // Load cluster configuration (optional)
        var clusterConfig = LoadClusterConfiguration();
        services.AddSingleton(clusterConfig);

        // Register DeepSeek provider based on config and/or environment variables
        var deepSeekConfig = chatConfig.Providers.DeepSeek;
        var deepSeekApiKeyEnv = deepSeekConfig?.ApiKeyEnvVar ?? "DEEPSEEK_API_KEY";
        var deepSeekApiKey = Environment.GetEnvironmentVariable(deepSeekApiKeyEnv) ?? deepSeekConfig?.ApiKey ?? string.Empty;

        if (deepSeekConfig is not null || !string.IsNullOrWhiteSpace(deepSeekApiKey))
        {
            services.AddHttpClient<DeepSeekChatProvider>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LORE-LLM/0.1");
            });
            services.AddSingleton<IChatProvider>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(nameof(DeepSeekChatProvider));
                var model = deepSeekConfig?.Model ?? "deepseek-chat";
                var temperature = deepSeekConfig?.Temperature;
                var maxTokens = deepSeekConfig?.MaxTokens;
                return new DeepSeekChatProvider(httpClient, deepSeekApiKey, model, temperature, maxTokens, deepSeekApiKeyEnv);
            });
        }

        services.AddSingleton<ICommandHandler<ExtractCommandOptions>, ExtractCommandHandler>();
        services.AddSingleton<ICommandHandler<AugmentCommandOptions>, AugmentCommandHandler>();
        services.AddSingleton<ICommandHandler<TranslateCommandOptions>, TranslateCommandHandler>();
        services.AddSingleton<ICommandHandler<ValidateCommandOptions>, ValidateCommandHandler>();
        services.AddSingleton<ICommandHandler<IntegrateCommandOptions>, IntegrateCommandHandler>();
        services.AddSingleton<ICommandHandler<InvestigationCommandOptions>, InvestigationCommandHandler>();
        services.AddSingleton<ICommandHandler<WikiCrawlCommandOptions>, WikiCrawlCommandHandler>();
        services.AddSingleton<ICommandHandler<IndexWikiCommandOptions>, IndexWikiCommandHandler>();

        services.AddSingleton<IWikiIndexService, WikiIndexService>();
        services.AddSingleton<IDeterministicEmbeddingProvider, HashEmbeddingProvider>();
        services.AddHttpClient("qdrant", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<ICommandHandler<ClusterCommandOptions>, ClusterCommandHandler>();

        return services;
    }

    private static ChatProvidersConfiguration LoadChatProvidersConfiguration()
    {
        try
        {
            var configDir = Environment.GetEnvironmentVariable("LORE_LLM_CONFIG_DIR");
            var baseDir = string.IsNullOrWhiteSpace(configDir) ? Directory.GetCurrentDirectory() : configDir!;
            var path = Path.Combine(baseDir, "config", "chat.providers.json");
            if (!File.Exists(path))
            {
                return new ChatProvidersConfiguration();
            }

            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var cfg = JsonSerializer.Deserialize<ChatProvidersConfiguration>(json, options);
            return cfg ?? new ChatProvidersConfiguration();
        }
        catch
        {
            return new ChatProvidersConfiguration();
        }
    }

    private static ClusterConfiguration LoadClusterConfiguration()
    {
        try
        {
            var configDir = Environment.GetEnvironmentVariable("LORE_LLM_CONFIG_DIR");
            var baseDir = string.IsNullOrWhiteSpace(configDir) ? Directory.GetCurrentDirectory() : configDir!;
            var path = Path.Combine(baseDir, "config", "cluster.json");
            if (!File.Exists(path))
            {
                return new ClusterConfiguration();
            }

            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var cfg = JsonSerializer.Deserialize<ClusterConfiguration>(json, options);
            return cfg ?? new ClusterConfiguration();
        }
        catch
        {
            return new ClusterConfiguration();
        }
    }
}
