using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using LORE_LLM.Application.Retrieval;
using Shouldly;
using Xunit;

namespace LORE_LLM.Tests.Retrieval;

public class QdrantClientTests
{
	[Fact]
	public async Task Search_builds_filter_when_keywords_present()
	{
		var handler = new StubHandler(async req =>
		{
			var json = await req.Content!.ReadAsStringAsync();
			json.ShouldContain("\"filter\"");
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("{\"result\":[]}", Encoding.UTF8, "application/json")
			};
		});
		var http = new HttpClient(handler);
		var client = new QdrantClient(http, "http://localhost:6333", null);
		var res = await client.SearchAsync("col", new float[] { 0.1f, 0.2f }, 5, new[] { "executor", "boddho" }, CancellationToken.None);
		res.IsSuccess.ShouldBeTrue();
		res.Value.Count.ShouldBe(0);
	}

	[Fact]
	public async Task Search_omits_filter_when_no_keywords()
	{
		var handler = new StubHandler(async req =>
		{
			var json = await req.Content!.ReadAsStringAsync();
			json.ShouldNotContain("\"filter\"");
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("{\"result\":[]}", Encoding.UTF8, "application/json")
			};
		});
		var http = new HttpClient(handler);
		var client = new QdrantClient(http, "http://localhost:6333", null);
		var res = await client.SearchAsync("col", new float[] { 0.1f, 0.2f }, 5, Array.Empty<string>(), CancellationToken.None);
		res.IsSuccess.ShouldBeTrue();
		res.Value.Count.ShouldBe(0);
	}

	private sealed class StubHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;
		public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) => _handler = handler;
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => _handler(request);
	}
}


