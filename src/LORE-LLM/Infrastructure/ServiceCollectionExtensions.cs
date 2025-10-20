using LORE_LLM.Application.Abstractions;
using LORE_LLM.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace LORE_LLM.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLoreLlmServices(this IServiceCollection services)
    {
        services.AddSingleton<ICliApplication, CliApplication>();
        return services;
    }
}
