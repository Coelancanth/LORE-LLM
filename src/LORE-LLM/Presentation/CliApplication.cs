using LORE_LLM.Application.Abstractions;

namespace LORE_LLM.Presentation;

public sealed class CliApplication : ICliApplication
{
    public Task<int> RunAsync(string[] args)
    {
        Console.WriteLine("LORE-LLM CLI scaffolding pending implementation.");
        return Task.FromResult(0);
    }
}
