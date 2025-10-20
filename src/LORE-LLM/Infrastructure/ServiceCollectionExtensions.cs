using LORE_LLM.Application.Abstractions;
using LORE_LLM.Application.Commands;
using LORE_LLM.Application.Commands.Augment;
using LORE_LLM.Application.Commands.Extract;
using LORE_LLM.Application.Commands.Integrate;
using LORE_LLM.Application.Commands.Translate;
using LORE_LLM.Application.Commands.Validate;
using LORE_LLM.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace LORE_LLM.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLoreLlmServices(this IServiceCollection services)
    {
        services.AddSingleton<ICliApplication, CliApplication>();

        services.AddSingleton<ICommandHandler<ExtractCommandOptions>, ExtractCommandHandler>();
        services.AddSingleton<ICommandHandler<AugmentCommandOptions>, AugmentCommandHandler>();
        services.AddSingleton<ICommandHandler<TranslateCommandOptions>, TranslateCommandHandler>();
        services.AddSingleton<ICommandHandler<ValidateCommandOptions>, ValidateCommandHandler>();
        services.AddSingleton<ICommandHandler<IntegrateCommandOptions>, IntegrateCommandHandler>();

        return services;
    }
}
