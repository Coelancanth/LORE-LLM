using LORE_LLM.Presentation;
using Shouldly;

namespace LORE_LLM.Tests;

public class CliApplicationTests
{
    [Fact]
    public async Task RunAsync_returns_success_code()
    {
        var cli = new CliApplication();

        var exitCode = await cli.RunAsync(Array.Empty<string>());

        exitCode.ShouldBe(0);
    }
}
