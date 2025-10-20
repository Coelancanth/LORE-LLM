using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LORE_LLM.Application.Wiki;
using LORE_LLM.Application.PostProcessing;
using LORE_LLM.Infrastructure;
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
        var crawler = new MediaWikiCrawler(client, new ProjectNameSanitizer());

        var result = await crawler.CrawlAsync(
            _workspace,
            "Pathologic2 Marble Nest",
            forceRefresh: true,
            specificPages: new[] { "Daniil Dankovsky" },
            maxPages: 0,
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1);

        var outputFile = Path.Combine(_projectDirectory.FullName, "knowledge", "raw", "daniil-dankovsky.md");
        File.Exists(outputFile).ShouldBeTrue("Expected crawler to create markdown file.");

        var contents = File.ReadAllText(outputFile);
        contents.ShouldContain("# Daniil Dankovsky");
        contents.ShouldContain("Source:");
        contents.ShouldContain("Masked enforcer");
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
                const string payload = "{\"parse\":{\"text\":\"<p>Masked enforcer of the Inquisitor.</p>\"}}";
                return Task.FromResult(Ok(payload));
            }

            if (uri.Contains("list=allpages", StringComparison.OrdinalIgnoreCase))
            {
                const string payload = "{\"query\":{\"allpages\":[{\"title\":\"Daniil Dankovsky\"}]}}";
                return Task.FromResult(Ok(payload));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage Ok(string payload) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
    }
}

