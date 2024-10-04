using System.Diagnostics;

namespace Highbyte.DotNet6502.AI.CodingAssistant.Inference.OpenAI;
public class DisableActivityHandler : DelegatingHandler
{
    /// <summary>
    /// Distributed tracing headers that is set automatically by .NET that local Ollama API CORS rules doesn't allow.
    /// </summary>
    static readonly List<string> s_HeadersToRemove = new List<string>
        {
            "x-ms-client-request-id",
            "x-ms-return-client-request-id"
        };

    public DisableActivityHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {

    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Note: A workaround by setting Activity.Current = null doesn't seem to work. Instead remove headers manually below.
        //Activity.Current = null;

        // Remove s_HeadersToRemove list of header from request if they exist.
        foreach (var headerName in s_HeadersToRemove)
        {
            if (request.Headers.Contains(headerName))
            {
                request.Headers.Remove(headerName);
                Activity.Current = null;
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
