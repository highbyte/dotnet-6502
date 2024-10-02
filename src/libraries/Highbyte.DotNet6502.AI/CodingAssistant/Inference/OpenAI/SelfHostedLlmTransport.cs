// Based on https://github.com/dotnet/smartcomponents

using Azure.Core;
using Azure.Core.Pipeline;

namespace Highbyte.DotNet6502.AI.CodingAssistant.Inference.OpenAI;

/// <summary>
/// Used to resolve queries using Ollama or anything else that exposes an OpenAI-compatible
/// endpoint with a scheme/host/port set of your choice.
/// </summary>
internal class SelfHostedLlmTransport : HttpClientTransport
{
    private readonly Uri _endpoint;

    internal SelfHostedLlmTransport(Uri endpoint) : base()
    {
        _endpoint = endpoint;
    }
    internal SelfHostedLlmTransport(Uri endpoint, HttpClient httpClient) : base(httpClient)
    {
        _endpoint = endpoint;
    }

    public override ValueTask ProcessAsync(HttpMessage message)
    {
        message.Request.Uri.Scheme = _endpoint.Scheme;
        message.Request.Uri.Host = _endpoint.Host;
        message.Request.Uri.Port = _endpoint.Port;
        return base.ProcessAsync(message);
    }
}
