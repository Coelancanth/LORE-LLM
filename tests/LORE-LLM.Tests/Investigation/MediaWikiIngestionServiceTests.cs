using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LORE_LLM.Application.Investigation;
using Shouldly;
using Xunit;

namespace LORE_LLM.Tests.Investigation;

public class MediaWikiIngestionServiceTests : IDisposable
{
    private readonly DirectoryInfo _tempDirectory;

    public MediaWikiIngestionServiceTests()
    {
        var path = Path.Combine(Path.GetTempPath(), "lore-llm-tests", "mediawiki", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirectory = new DirectoryInfo(path);
    }

    [Fact]
    public async Task EnsureKnowledgeBaseAsync_fetches_entries_and_caches_results()
    {
        var handler = new StubHttpMessageHandler();
        var client = new HttpClient(handler);
        var service = new MediaWikiIngestionService(client);

        var tokens = new[] { "Executor" };

        var result = await service.EnsureKnowledgeBaseAsync(
            _tempDirectory,
            "pathologic2-marble-nest",
            "Pathologic2 Marble Nest",
            tokens,
            forceRefresh: true,
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Entries.Count.ShouldBe(1);
        result.Value.Entries[0].Title.ShouldBe("Executor");
        handler.RequestCount.ShouldBe(2); // opensearch + parse

        handler.Reset();

        var cachedResult = await service.EnsureKnowledgeBaseAsync(
            _tempDirectory,
            "pathologic2-marble-nest",
            "Pathologic2 Marble Nest",
            tokens,
            forceRefresh: false,
            CancellationToken.None);

        cachedResult.IsSuccess.ShouldBeTrue();
        cachedResult.Value.Entries.Count.ShouldBe(1);
        handler.RequestCount.ShouldBe(0);
    }

    public void Dispose()
    {
        if (_tempDirectory.Exists)
        {
            try
            {
                _tempDirectory.Delete(recursive: true);
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;

            if (request.RequestUri is null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
            }

            var uri = request.RequestUri.ToString();
            if (uri.Contains("opensearch", StringComparison.OrdinalIgnoreCase))
            {
                var payload = "[\"Executor\",[\"Executor\"],[\"\"],[\"https://pathologic.fandom.com/wiki/Executor\"]]";
                return Task.FromResult(BuildResponse(payload));
            }

            if (uri.Contains("action=parse", StringComparison.OrdinalIgnoreCase))
            {
                var payload = "{\"parse\":{\"text\":\"<p>The Executor is a masked enforcer.</p>\"}}";
                return Task.FromResult(BuildResponse(payload));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        public void Reset() => RequestCount = 0;

        private static HttpResponseMessage BuildResponse(string payload)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        }
    }
}
