namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// Result of an HTTP request made from a Lua script.
/// </summary>
public sealed record HttpProxyResponse(
    bool Ok,
    int Status,
    string? Body,
    byte[]? BodyBytes,
    string? Error);

/// <summary>
/// Provides outbound HTTP operations to Lua scripts via the <c>http</c> global table.
/// A single <see cref="HttpClient"/> instance is shared for all calls made by a scripting session.
/// </summary>
public sealed class LuaHttpProxy : IDisposable
{
    private readonly HttpClient _client;

    public LuaHttpProxy(TimeSpan? timeout = null)
    {
        _client = new HttpClient
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Performs a GET request and returns the response body as a string.
    /// </summary>
    public HttpProxyResponse GetString(string url, Dictionary<string, string>? headers = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyHeaders(request, headers);
            using var response = _client.Send(request);
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var status = (int)response.StatusCode;
            return response.IsSuccessStatusCode
                ? new HttpProxyResponse(true, status, body, null, null)
                : new HttpProxyResponse(false, status, null, null, $"HTTP {status}");
        }
        catch (Exception ex)
        {
            return new HttpProxyResponse(false, 0, null, null, ex.Message);
        }
    }

    /// <summary>
    /// Performs a GET request and returns the response body as a byte array.
    /// </summary>
    public HttpProxyResponse GetBytes(string url, Dictionary<string, string>? headers = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyHeaders(request, headers);
            using var response = _client.Send(request);
            var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            var status = (int)response.StatusCode;
            return response.IsSuccessStatusCode
                ? new HttpProxyResponse(true, status, null, bytes, null)
                : new HttpProxyResponse(false, status, null, null, $"HTTP {status}");
        }
        catch (Exception ex)
        {
            return new HttpProxyResponse(false, 0, null, null, ex.Message);
        }
    }

    /// <summary>
    /// Performs a POST request with the given body and content type, returning the response body as a string.
    /// </summary>
    public HttpProxyResponse Post(string url, string body, string contentType, Dictionary<string, string>? headers = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, contentType)
            };
            ApplyHeaders(request, headers);
            using var response = _client.Send(request);
            var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var status = (int)response.StatusCode;
            return response.IsSuccessStatusCode
                ? new HttpProxyResponse(true, status, responseBody, null, null)
                : new HttpProxyResponse(false, status, null, null, $"HTTP {status}");
        }
        catch (Exception ex)
        {
            return new HttpProxyResponse(false, 0, null, null, ex.Message);
        }
    }

    /// <summary>
    /// Performs a GET request and streams the response body directly to <paramref name="absoluteFilePath"/>.
    /// The caller is responsible for ensuring the path is safe (within the sandbox).
    /// </summary>
    public HttpProxyResponse DownloadToFile(string url, string absoluteFilePath, Dictionary<string, string>? headers = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyHeaders(request, headers);
            using var response = _client.Send(request, HttpCompletionOption.ResponseHeadersRead);
            var status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
                return new HttpProxyResponse(false, status, null, null, $"HTTP {status}");
            using var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            using var file = File.Create(absoluteFilePath);
            stream.CopyTo(file);
            return new HttpProxyResponse(true, status, null, null, null);
        }
        catch (Exception ex)
        {
            return new HttpProxyResponse(false, 0, null, null, ex.Message);
        }
    }

    private static void ApplyHeaders(HttpRequestMessage request, Dictionary<string, string>? headers)
    {
        if (headers == null) return;
        foreach (var (key, value) in headers)
            request.Headers.TryAddWithoutValidation(key, value);
    }

    public void Dispose() => _client.Dispose();
}
