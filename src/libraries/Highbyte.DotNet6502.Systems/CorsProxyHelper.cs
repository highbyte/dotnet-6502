namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Helper for routing browser (WebAssembly) HTTP fetches through a CORS proxy.
/// <para>
/// A WebAssembly app can only fetch cross-origin resources that send permissive CORS headers;
/// arbitrary third-party URLs (e.g. a user-supplied <c>.prg</c> / <c>.d64</c> link) usually do not,
/// so they must be routed through a proxy that adds the headers. Same-origin resources, relative
/// URLs, and URLs that are already proxied need no wrapping.
/// </para>
/// </summary>
public static class CorsProxyHelper
{
    /// <summary>
    /// Returns <paramref name="url"/> wrapped with <paramref name="corsProxyUrl"/> when it is a
    /// cross-origin absolute HTTP(S) URL that is not already proxied; otherwise returns it unchanged.
    /// </summary>
    /// <param name="url">The resource URL the caller wants to fetch (absolute or relative).</param>
    /// <param name="corsProxyUrl">
    /// The CORS proxy prefix (e.g. <c>https://proxy/fetch?url=</c>). <see langword="null"/> / empty
    /// disables proxying entirely — the right value on desktop, where direct fetches are unrestricted.
    /// </param>
    /// <param name="appBaseUrl">
    /// The app's own origin (absolute URL). When supplied, a URL on the same origin is treated as
    /// same-origin and left unwrapped. When <see langword="null"/>, only relative URLs are treated as
    /// same-origin.
    /// </param>
    public static string ApplyCorsProxyIfNeeded(string url, string? corsProxyUrl, string? appBaseUrl = null)
    {
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(corsProxyUrl))
            return url;

        // Already routed through this proxy — don't double-wrap.
        if (url.StartsWith(corsProxyUrl, StringComparison.OrdinalIgnoreCase))
            return url;

        // Relative URL → same-origin, no proxy needed.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return url;

        // Only HTTP(S) is proxied (leave data:, blob:, etc. untouched).
        if (absolute.Scheme != Uri.UriSchemeHttp && absolute.Scheme != Uri.UriSchemeHttps)
            return url;

        // Same-origin as the app → no proxy needed.
        if (!string.IsNullOrEmpty(appBaseUrl)
            && Uri.TryCreate(appBaseUrl, UriKind.Absolute, out var baseUri)
            && string.Equals(
                absolute.GetLeftPart(UriPartial.Authority),
                baseUri.GetLeftPart(UriPartial.Authority),
                StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        return corsProxyUrl + Uri.EscapeDataString(url);
    }
}
