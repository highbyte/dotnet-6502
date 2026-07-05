using System.Net;
using System.Text;
using Highbyte.DotNet6502.Updates;
using Xunit;

namespace Highbyte.DotNet6502.Updates.Tests;

public class GitHubReleaseClientTests
{
    /// <summary>Scripted handler capturing each request's If-None-Match and returning queued responses.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public List<string?> ReceivedIfNoneMatch { get; } = new();

        public StubHandler(params HttpResponseMessage[] responses) => _responses = new Queue<HttpResponseMessage>(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ReceivedIfNoneMatch.Add(request.Headers.IfNoneMatch.ToString());
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private static HttpResponseMessage Ok(string weakEtag, string body)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        response.Headers.TryAddWithoutValidation("ETag", weakEtag);
        return response;
    }

    [Fact]
    public async Task WeakETag_IsStoredThenSentAsConditionalRequest()
    {
        const string weakEtag = "W/\"7084393f0b2cf3b5ca133bafbbcd6135\"";
        const string body = "[{\"tag_name\":\"v0.40.2-alpha\",\"prerelease\":true,\"html_url\":\"https://example/x\"}]";

        var handler = new StubHandler(
            Ok(weakEtag, body),
            new HttpResponseMessage(HttpStatusCode.NotModified));
        var client = new GitHubReleaseClient(new HttpClient(handler));

        // 1st call: no ETag sent, gets the weak ETag back.
        var first = await client.GetReleasesAsync(null);
        Assert.False(first.NotModified);
        Assert.Equal(weakEtag, first.ETag);
        Assert.Single(first.Releases);
        Assert.Equal("v0.40.2-alpha", first.Releases[0].TagName);

        // 2nd call: sends the weak ETag back (must not throw), server answers 304.
        var second = await client.GetReleasesAsync(first.ETag);
        Assert.True(second.NotModified);
        Assert.Equal(weakEtag, handler.ReceivedIfNoneMatch[1]);
    }
}
