using System.Net;

namespace Highbyte.DotNet6502.Utils;

public static class DownloadErrorHelper
{
    public static string BuildDownloadFailureMessage(
        string resourceName,
        string? sourceUrl,
        string? requestUrl,
        Exception exception)
    {
        var rootException = exception.GetBaseException();
        var sourceDisplay = GetEndpointDisplay(sourceUrl);
        var requestDisplay = GetEndpointDisplay(requestUrl);

        if (IsBrowserFetchFailure(rootException))
        {
            if (!string.IsNullOrEmpty(requestDisplay) &&
                !string.Equals(sourceDisplay, requestDisplay, StringComparison.OrdinalIgnoreCase))
            {
                return $"Failed to download {resourceName} from {sourceDisplay}. The browser could not fetch it via {requestDisplay}. The source site or CORS proxy may be unavailable. Try again later or change the CORS proxy setting.";
            }

            return $"Failed to download {resourceName} from {sourceDisplay}. The browser could not fetch it. The site may be unavailable, blocked by CORS, or the network request was interrupted.";
        }

        if (rootException is TaskCanceledException)
        {
            return $"Timed out while downloading {resourceName} from {sourceDisplay}. Try again later.";
        }

        if (rootException is HttpRequestException httpRequestException && httpRequestException.StatusCode is HttpStatusCode statusCode)
        {
            return $"Failed to download {resourceName} from {sourceDisplay}. The server returned HTTP {(int)statusCode} ({statusCode}).";
        }

        var detail = rootException.Message;
        if (string.IsNullOrWhiteSpace(detail))
        {
            return $"Failed to download {resourceName} from {sourceDisplay}.";
        }

        return $"Failed to download {resourceName} from {sourceDisplay}. {detail}";
    }

    public static string FlattenExceptionMessages(Exception exception)
    {
        var messages = new List<string>();
        var seenMessages = new HashSet<string>(StringComparer.Ordinal);

        for (var current = exception; current != null; current = current.InnerException)
        {
            if (string.IsNullOrWhiteSpace(current.Message))
                continue;

            if (seenMessages.Add(current.Message))
            {
                messages.Add(current.Message);
            }
        }

        return messages.Count == 0 ? exception.GetType().Name : string.Join(" --> ", messages);
    }

    private static bool IsBrowserFetchFailure(Exception exception)
        => exception.Message.Contains("Failed to fetch", StringComparison.OrdinalIgnoreCase);

    private static string GetEndpointDisplay(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "the configured source";

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            return uri.Host;

        return url;
    }
}
