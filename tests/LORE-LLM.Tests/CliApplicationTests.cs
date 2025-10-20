using LORE_LLM.Application.Abstractions;
using LORE_LLM.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace LORE_LLM.Tests;

public class CliApplicationTests
{
    [Fact]
    public async Task RunAsync_without_arguments_returns_help_success()
    {
        var cli = CreateCliApplication();

        var exitCode = await cli.RunAsync(Array.Empty<string>());

        exitCode.ShouldBe(1);
    }

    public static IEnumerable<object[]> CommandNames()
    {
        yield return new object[] { "extract" };
        yield return new object[] { "augment" };
        yield return new object[] { "translate" };
        yield return new object[] { "validate" };
        yield return new object[] { "integrate" };
    }

    [Theory]
    [MemberData(nameof(CommandNames))]
    public async Task Commands_return_success(string commandName)
    {
        var cli = CreateCliApplication();
        var args = BuildArguments(commandName);

        var exitCode = await cli.RunAsync(args);

        exitCode.ShouldBe(0);
    }

    private static ICliApplication CreateCliApplication()
    {
        var services = new ServiceCollection();
        services.AddLoreLlmServices();
        return services.BuildServiceProvider().GetRequiredService<ICliApplication>();
    }

    private static string[] BuildArguments(string commandName)
    {
        var workspace = CreateTempDirectory();

        return commandName switch
        {
            "extract" => new[]
            {
                "extract",
                "--input", CreateTempFile(),
                "--output", workspace
            },
            "augment" => new[]
            {
                "augment",
                "--workspace", workspace
            },
            "translate" => new[]
            {
                "translate",
                "--workspace", workspace,
                "--language", "ru"
            },
            "validate" => new[]
            {
                "validate",
                "--workspace", workspace
            },
            "integrate" => new[]
            {
                "integrate",
                "--workspace", workspace,
                "--destination", CreateTempDirectory()
            },
            _ => throw new ArgumentOutOfRangeException(nameof(commandName), commandName, null)
        };
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "lore-llm-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateTempFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "lore-llm-tests", Guid.NewGuid().ToString("N") + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "stub");
        return filePath;
    }
}
