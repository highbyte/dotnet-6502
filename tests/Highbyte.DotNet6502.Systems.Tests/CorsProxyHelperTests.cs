namespace Highbyte.DotNet6502.Systems.Tests;

public class CorsProxyHelperTests
{
    private const string Proxy = "https://proxy.example/fetch?url=";
    private const string AppBase = "https://app.example/dotnet-6502/";

    [Fact]
    public void NullOrEmptyProxy_ReturnsUrlUnchanged()
    {
        const string url = "https://other.example/game.prg";
        Assert.Equal(url, CorsProxyHelper.ApplyCorsProxyIfNeeded(url, null, AppBase));
        Assert.Equal(url, CorsProxyHelper.ApplyCorsProxyIfNeeded(url, "", AppBase));
    }

    [Fact]
    public void CrossOriginAbsoluteUrl_IsWrapped()
    {
        const string url = "https://other.example/game.prg";
        var result = CorsProxyHelper.ApplyCorsProxyIfNeeded(url, Proxy, AppBase);
        Assert.Equal(Proxy + Uri.EscapeDataString(url), result);
    }

    [Fact]
    public void CrossOriginAbsoluteUrl_IsWrapped_WhenNoAppBaseGiven()
    {
        const string url = "https://other.example/game.d64";
        var result = CorsProxyHelper.ApplyCorsProxyIfNeeded(url, Proxy);
        Assert.Equal(Proxy + Uri.EscapeDataString(url), result);
    }

    [Fact]
    public void RelativeUrl_IsNotWrapped()
    {
        const string url = "prg/c64/demo.prg";
        Assert.Equal(url, CorsProxyHelper.ApplyCorsProxyIfNeeded(url, Proxy, AppBase));
    }

    [Fact]
    public void SameOriginAbsoluteUrl_IsNotWrapped()
    {
        const string url = "https://app.example/dotnet-6502/prg/c64/demo.prg";
        Assert.Equal(url, CorsProxyHelper.ApplyCorsProxyIfNeeded(url, Proxy, AppBase));
    }

    [Fact]
    public void AlreadyProxiedUrl_IsNotDoubleWrapped()
    {
        var url = Proxy + Uri.EscapeDataString("https://other.example/game.prg");
        Assert.Equal(url, CorsProxyHelper.ApplyCorsProxyIfNeeded(url, Proxy, AppBase));
    }

    [Fact]
    public void NonHttpScheme_IsNotWrapped()
    {
        const string url = "data:application/octet-stream;base64,AAAA";
        Assert.Equal(url, CorsProxyHelper.ApplyCorsProxyIfNeeded(url, Proxy, AppBase));
    }
}
