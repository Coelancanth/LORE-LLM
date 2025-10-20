using LORE_LLM.Application.Abstractions;
using LORE_LLM.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLoreLlmServices();

using var host = builder.Build();

var application = host.Services.GetRequiredService<ICliApplication>();

return await application.RunAsync(args);
