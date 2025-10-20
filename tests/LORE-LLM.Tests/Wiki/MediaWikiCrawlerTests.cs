using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LORE_LLM.Application.PostProcessing;
using LORE_LLM.Application.Wiki;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace LORE_LLM.Tests.Wiki;

public sealed class MediaWikiCrawlerTests : IDisposable
{
    private readonly DirectoryInfo _workspace;
    private readonly DirectoryInfo _projectDirectory;

    public MediaWikiCrawlerTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "lore-llm-tests", "crawler", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        _workspace = new DirectoryInfo(root);

        var sanitizer = new ProjectNameSanitizer();
        var sanitizedProject = sanitizer.Sanitize("Pathologic2 Marble Nest");
        _projectDirectory = new DirectoryInfo(Path.Combine(_workspace.FullName, sanitizedProject));
        _projectDirectory.Create();
    }

    [Fact]
    public async Task CrawlAsync_writes_markdown_files_for_requested_pages()
    {
        var handler = new StubHttpMessageHandler();
        var client = new HttpClient(handler);
        var pipeline = new MediaWikiHtmlPostProcessingPipeline(new IMediaWikiHtmlPostProcessor[]
        {
            new CommonMediaWikiHtmlPostProcessor(),
            new PathologicMarbleNestHtmlPostProcessor()
        });
        var projectOptions = new MediaWikiCrawlerProjectOptions
        {
            ApiBase = "https://example.invalid/api.php"
        };
        projectOptions.EmitBaseDocument = false;
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
        var crawlerOptions = new MediaWikiCrawlerOptions();
        crawlerOptions.Projects["pathologic2-marble-nest"] = projectOptions;
        var options = Options.Create(crawlerOptions);
        var crawler = new MediaWikiCrawler(client, new ProjectNameSanitizer(), pipeline, options);

        var result = await crawler.CrawlAsync(
            _workspace,
            "Pathologic2 Marble Nest",
            forceRefresh: true,
            specificPages: new[] { "Bachelor" },
            maxPages: 0,
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1);

        var outputFile = Path.Combine(_projectDirectory.FullName, "knowledge", "raw", "bachelor.md");
        File.Exists(outputFile).ShouldBeFalse("Combined markdown should be omitted when tab variants are configured.");

        var tabPathologic2 = Path.Combine(_projectDirectory.FullName, "knowledge", "raw", "bachelor-pathologic-2.md");
        File.Exists(tabPathologic2).ShouldBeTrue("Expected crawler to create Pathologic 2 tab variant.");
        var tabPathologic2Content = File.ReadAllText(tabPathologic2);
        tabPathologic2Content.ShouldContain("# Bachelor (Pathologic 2)");
        tabPathologic2Content.ShouldContain("Masked enforcer");
        tabPathologic2Content.ShouldNotContain("## Pathologic 2");
        tabPathologic2Content.ShouldNotContain("![Portrait]");

        var tabPathologic = Path.Combine(_projectDirectory.FullName, "knowledge", "raw", "bachelor-pathologic.md");
        File.Exists(tabPathologic).ShouldBeTrue("Expected crawler to create Pathologic tab variant.");
        var tabPathologicContent = File.ReadAllText(tabPathologic);
        tabPathologicContent.ShouldContain("# Bachelor (Pathologic)");
        tabPathologicContent.ShouldNotContain("Should be dropped");
        tabPathologicContent.ShouldNotContain("## Pathologic");
    }

    public void Dispose()
    {
        if (_workspace.Exists)
        {
            try
            {
                _workspace.Delete(recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
            }

            var uri = request.RequestUri.ToString();
            if (uri.Contains("action=parse", StringComparison.OrdinalIgnoreCase))
            {
                if (uri.Contains("page=Daniil%20Dankovsky", StringComparison.OrdinalIgnoreCase) ||
                    uri.Contains("page=Daniil+Dankovsky", StringComparison.OrdinalIgnoreCase))
                {
                    const string redirectHtml = "<div class='mw-content-ltr mw-parser-output'>" +
                                                "<div class='redirectMsg'><p>Redirect to:</p><ul class='redirectText'><li><a href='/wiki/Bachelor' title='Bachelor'>Bachelor</a></li></ul></div>" +
                                                "</div>";
                    var redirectPayload = $"{{\"parse\":{{\"text\":\"{EscapeForJson(redirectHtml)}\"}}}}";
                    return Task.FromResult(Ok(redirectPayload));
                }

                if (uri.Contains("page=Bachelor", StringComparison.OrdinalIgnoreCase))
                {
                    const string bachelorHtml = "<div class='mw-content-ltr mw-parser-output'>" +
                                                "<div class='tabber wds-tabber'>" +
                                                "  <div class='wds-tabs__wrapper with-bottom-border'>" +
                                                "    <ul class='wds-tabs'>" +
                                                "      <li class='wds-tabs__tab wds-is-current' data-hash='Pathologic_2'>" +
                                                "        <div class='wds-tabs__tab-label'><a href='#'>Pathologic 2</a></div>" +
                                                "      </li>" +
                                                "      <li class='wds-tabs__tab' data-hash='Pathologic'>" +
                                                "        <div class='wds-tabs__tab-label'><a href='#'>Pathologic</a></div>" +
                                                "      </li>" +
                                                "    </ul>" +
                                                "  </div>" +
                                                "  <div class='wds-tab__content wds-is-current'>" +
                                                "    <div class='nomobile'><table class='plainlinks ambox'><tr><td>spoiler</td></tr></table></div>" +
                                                "    <p>Masked enforcer of the Inquisitor.</p>" +
                                                "  </div>" +
                                                "  <div class='wds-tab__content'>" +
                                                "    <table class='infoboxtable'><tr><td>Should be removed</td></tr></table>" +
                                                "    <p>Alternate timeline biography.</p>" +
                                                "    <img src='portrait.png' alt='Portrait' />" +
                                                "    <table class='navbox'><tr><td>Should be dropped</td></tr></table>" +
                                                "  </div>" +
                                                "</div>" +
                                                "</div>";
                    var bachelorPayload = $"{{\"parse\":{{\"text\":\"{EscapeForJson(bachelorHtml)}\"}}}}";
                    return Task.FromResult(Ok(bachelorPayload));
                }
            }

            if (uri.Contains("list=allpages", StringComparison.OrdinalIgnoreCase))
            {
                const string payload = "{\"query\":{\"allpages\":[{\"title\":\"Bachelor\"}]}}";
                return Task.FromResult(Ok(payload));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage Ok(string payload) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

        private static string EscapeForJson(string value) =>
            value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", string.Empty)
                .Replace("\n", "\\n");
    }
}



