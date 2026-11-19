using System.Net;
using System.Text;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>Tests for the Tavily web-search provider and its configuration.</summary>
public sealed class WebSearchTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task SearchMapsTavilyResults()
    {
        var handler = new Handler(_ => Json("""
            {"results":[
              {"title":"缓存一致性","url":"https://example.com/a","content":"读写时序","published_date":"2026-01-01"},
              {"title":"缺少正文","url":"https://example.com/b"},
              {"title":"无链接","content":"正文"}
            ]}
            """));
        var provider = new TavilySearchProvider(new HttpClient(handler), "key");
        var response = await provider.SearchAsync("缓存一致性", 3);
        Assert.Null(response.Error);
        Assert.Equal("tavily", response.Provider);
        var result = Assert.Single(response.Results);
        Assert.Equal("缓存一致性", result.Title);
        Assert.Equal("https://example.com/a", result.Url);
        Assert.Equal("读写时序", result.Snippet);
    }

    [Fact]
    public async Task SearchSendsTheKeyQueryAndClampedCount()
    {
        var handler = new Handler(_ => Json("""{"results":[]}"""));
        var provider = new TavilySearchProvider(new HttpClient(handler), "secret");
        await provider.SearchAsync("问题", 99);
        Assert.NotNull(handler.Body);
        using var body = System.Text.Json.JsonDocument.Parse(handler.Body!);
        var root = body.RootElement;
        Assert.Equal("secret", root.GetProperty("api_key").GetString());
        Assert.Equal("问题", root.GetProperty("query").GetString());
        Assert.Equal(10, root.GetProperty("max_results").GetInt32());
    }

    [Fact]
    public async Task HttpFailuresBecomeAnErrorWithoutThrowing()
    {
        var handler = new Handler(_ => Json("""{"detail":"bad key"}""", HttpStatusCode.Unauthorized));
        var response = await new TavilySearchProvider(new HttpClient(handler), "bad").SearchAsync("q", 3);
        Assert.Empty(response.Results);
        Assert.NotNull(response.Error);
        Assert.Contains("401", response.Error);
    }

    [Fact]
    public void ConfigurationRequiresAKeyAndHonoursTheKillSwitch()
    {
        Assert.Equal((null, false), WebSearchConfiguration.FromEnvironment(_ => null));
        Assert.Equal(("k", true), WebSearchConfiguration.FromEnvironment(name => name == "TAVILY_API_KEY" ? "k" : null));
        Assert.Equal(("k", false), WebSearchConfiguration.FromEnvironment(name => name switch
        {
            "TAVILY_API_KEY" => "k",
            "NEXTTHOUGHT_WEB_SEARCH" => "off",
            _ => null
        }));
        Assert.Null(WebSearchConfiguration.CreateProvider(read: _ => null));
        Assert.IsType<TavilySearchProvider>(WebSearchConfiguration.CreateProvider(
            new HttpClient(new Handler(_ => Json("""{"results":[]}"""))), name => name == "TAVILY_API_KEY" ? "k" : null));
    }
}
