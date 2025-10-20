using LORE_LLM.Application.Abstractions;
using LORE_LLM.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace LORE_LLM.Tests;

public class CliApplicationTests
{
    [Fact]
    public async Task RunAsync_without_arguments_returns_help_error_code()
    {
        var cli = CreateCliApplication();

        var exitCode = await cli.RunAsync(Array.Empty<string>());

        exitCode.ShouldBe(1);
    }

    public static IEnumerable<object[]> CommandScenarios()
    {
        yield return new object[] { BuildExtractScenario() };
        yield return new object[] { BuildSimpleScenario("augment", "--workspace", CreateTempDirectory()) };
        yield return new object[] { BuildSimpleScenario("translate", "--workspace", CreateTempDirectory(), "--language", "ru") };
        yield return new object[] { BuildSimpleScenario("validate", "--workspace", CreateTempDirectory()) };
        yield return new object[] { BuildSimpleScenario("integrate", "--workspace", CreateTempDirectory(), "--destination", CreateTempDirectory()) };
    }

    [Theory]
    [MemberData(nameof(CommandScenarios))]
    public async Task Commands_return_success(CommandScenario scenario)
    {
        var cli = CreateCliApplication();

        var exitCode = await cli.RunAsync(scenario.Arguments);

        exitCode.ShouldBe(0);
        scenario.AssertPostConditions();
    }

    private static CommandScenario BuildExtractScenario()
    {
        var inputFile = CreateTempFile("1111 Sample line", "2222");
        var workspace = CreateTempDirectory();
        const string projectName = "pathologic2-marble-nest";

        var args = new[]
        {
            "extract",
            "--input", inputFile,
            "--output", workspace,
            "--project", projectName
        };

        return new CommandScenario(args, () =>
        {
            var projectFolder = Path.Combine(workspace, projectName);
            var rawPath = Path.Combine(projectFolder, "source_text_raw.json");
            var manifestPath = Path.Combine(projectFolder, "workspace.json");

            File.Exists(rawPath).ShouldBeTrue("Expected extractor to create source_text_raw.json");
            File.Exists(manifestPath).ShouldBeTrue("Expected extractor to create workspace.json");
        });
    }

    private static CommandScenario BuildSimpleScenario(params string[] arguments)
    {
        return new CommandScenario(arguments, () => { });
    }

    private static ICliApplication CreateCliApplication()
    {
        var services = new ServiceCollection();
        services.AddLoreLlmServices();
        return services.BuildServiceProvider().GetRequiredService<ICliApplication>();
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "lore-llm-tests", "cli", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateTempFile(params string[] lines)
    {
        var path = Path.Combine(Path.GetTempPath(), "lore-llm-tests", "cli", Guid.NewGuid().ToString("N") + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines);
        return path;
    }

    public sealed record CommandScenario(string[] Arguments, Action AssertPostConditions);
}
