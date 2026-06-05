using System.Net;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Tests.Utils;

public class DownloadErrorHelperTest
{
    [Fact]
    public void BuildDownloadFailureMessage_ExplainsBrowserFetchFailuresWithProxyContext()
    {
        var ex = new HttpRequestException("TypeError: Failed to fetch");

        var message = DownloadErrorHelper.BuildDownloadFailureMessage(
            "disk image 'Giana Sisters'",
            "https://csdb.dk/release/download.php?id=161456",
            "https://api.codetabs.com/v1/proxy?quest=https%3A%2F%2Fcsdb.dk%2Frelease%2Fdownload.php%3Fid%3D161456",
            ex);

        Assert.Contains("disk image 'Giana Sisters'", message);
        Assert.Contains("csdb.dk", message);
        Assert.Contains("api.codetabs.com", message);
        Assert.Contains("CORS proxy", message);
    }

    [Fact]
    public void BuildDownloadFailureMessage_ReportsHttpStatusCodes()
    {
        var ex = new HttpRequestException("Not found", null, HttpStatusCode.NotFound);

        var message = DownloadErrorHelper.BuildDownloadFailureMessage(
            "ROM 'kernal'",
            "https://example.com/kernal.rom",
            "https://example.com/kernal.rom",
            ex);

        Assert.Contains("HTTP 404", message);
        Assert.Contains("NotFound", message);
    }

    [Fact]
    public void FlattenExceptionMessages_DeduplicatesRepeatedMessages()
    {
        var ex = new InvalidOperationException(
            "Outer failure",
            new Exception("TypeError: Failed to fetch", new Exception("TypeError: Failed to fetch")));

        var message = DownloadErrorHelper.FlattenExceptionMessages(ex);

        Assert.Equal("Outer failure --> TypeError: Failed to fetch", message);
    }
}
