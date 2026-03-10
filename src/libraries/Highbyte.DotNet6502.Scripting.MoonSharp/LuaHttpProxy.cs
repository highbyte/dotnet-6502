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
/// Provides outbound async HTTP operations to Lua scripts via the <c>http</c> global table.
/// All methods are async so they work on both desktop and browser/WASM.
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

    public async Task<HttpProxyResponse> GetStringAsync(string url, Dictionary<string, string>? headers = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyHeaders(request, headers);
            using var response = await _client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
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

    public async Task<HttpProxyResponse> GetBytesAsync(string url, Dictionary<string, string>? headers = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyHeaders(request, headers);
            using var response = await _client.SendAsync(request);
            var bytes = await response.Content.ReadAsByteArrayAsync();
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

    public async Task<HttpProxyResponse> PostAsync(string url, string body, string contentType, Dictionary<string, string>? headers = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, contentType)
            };
            ApplyHeaders(request, headers);
            using var response = await _client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
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

    public async Task<HttpProxyResponse> DownloadToFileAsync(string url, string absoluteFilePath, Dictionary<string, string>? headers = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyHeaders(request, headers);
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            var status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
                return new HttpProxyResponse(false, status, null, null, $"HTTP {status}");
            using var stream = await response.Content.ReadAsStreamAsync();
            using var file = File.Create(absoluteFilePath);
            await stream.CopyToAsync(file);
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
