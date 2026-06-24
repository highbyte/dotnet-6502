using System.Text;
using Highbyte.DotNet6502.Systems.Commodore64.Sharing;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

public class C64ShareLinkBuilderTests
{
    private const string BaseUrl = "https://example.com/app/";

    private static Dictionary<string, string> ParseQuery(string url)
    {
        var queryStart = url.IndexOf('?');
        var query = queryStart >= 0 ? url[(queryStart + 1)..] : url;
        return query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty);
    }

    private static string Base64UrlDecode(string value)
    {
        var standard = value.Replace('-', '+').Replace('_', '/');
        standard = (standard.Length % 4) switch
        {
            2 => standard + "==",
            3 => standard + "=",
            _ => standard,
        };
        return Encoding.UTF8.GetString(Convert.FromBase64String(standard));
    }

    [Fact]
    public void Build_Always_Emits_System_Variant_Start_And_WaitForSystemReady()
    {
        var request = new C64ShareLinkRequest
        {
            Mode = C64ShareMode.CurrentBasic,
            SystemVariant = "C64PAL",
            BasicText = "10 print \"hi\"\n",
        };

        var url = C64ShareLinkBuilder.Build(BaseUrl, request);
        var q = ParseQuery(url);

        Assert.StartsWith(BaseUrl + "?", url);
        Assert.Equal("C64", q["system"]);
        Assert.Equal("C64PAL", q["systemVariant"]);
        Assert.Equal("1", q["start"]);
        Assert.Equal("1", q["waitForSystemReady"]);
    }

    [Fact]
    public void Build_CurrentBasic_Encodes_BasicText_As_Base64Url_And_RoundTrips()
    {
        var basic = "10 c1=7:c2=14\n20 print \"hello world!\"\n";
        var request = new C64ShareLinkRequest
        {
            Mode = C64ShareMode.CurrentBasic,
            SystemVariant = "C64NTSC",
            AutoRun = true,
            BasicText = basic,
        };

        var q = ParseQuery(C64ShareLinkBuilder.Build(BaseUrl, request));

        Assert.Equal("1", q["runBasic"]);
        Assert.Equal(basic, Base64UrlDecode(q["basicText"]));
        // base64url must not contain characters that need percent-encoding.
        Assert.DoesNotContain('+', q["basicText"]);
        Assert.DoesNotContain('/', q["basicText"]);
        Assert.DoesNotContain('=', q["basicText"]);
    }

    [Fact]
    public void Build_CurrentBasic_Without_AutoRun_Omits_RunBasic()
    {
        var request = new C64ShareLinkRequest
        {
            Mode = C64ShareMode.CurrentBasic,
            SystemVariant = "C64NTSC",
            AutoRun = false,
            BasicText = "10 print 1\n",
        };

        var q = ParseQuery(C64ShareLinkBuilder.Build(BaseUrl, request));

        Assert.False(q.ContainsKey("runBasic"));
    }

    [Fact]
    public void Build_DownloadProgram_Prg_Emits_LoadPrgUrl()
    {
        var request = new C64ShareLinkRequest
        {
            Mode = C64ShareMode.DownloadProgram,
            SystemVariant = "C64PAL",
            AutoRun = true,
            DownloadUrl = "https://compunet.live/static/compunet-reborn-live.prg",
            DownloadType = C64DownloadProgramType.Prg,
        };

        var q = ParseQuery(C64ShareLinkBuilder.Build(BaseUrl, request));

        Assert.Equal("https://compunet.live/static/compunet-reborn-live.prg", q["loadPrgUrl"]);
        Assert.Equal("1", q["runLoadedProgram"]);
        Assert.False(q.ContainsKey("loadD64Url"));
        Assert.False(q.ContainsKey("diskMount"));
    }

    [Fact]
    public void Build_DownloadProgram_D64Zip_DirectLoad_Emits_LoadD64Url_And_D64Program()
    {
        var request = new C64ShareLinkRequest
        {
            Mode = C64ShareMode.DownloadProgram,
            SystemVariant = "C64PAL",
            AutoRun = true,
            DownloadUrl = "https://csdb.dk/release/download.php?id=70413",
            DownloadType = C64DownloadProgramType.D64Zip,
            DirectLoadPRGName = "*",
        };

        var q = ParseQuery(C64ShareLinkBuilder.Build(BaseUrl, request));

        Assert.Equal("https://csdb.dk/release/download.php?id=70413", q["loadD64Url"]);
        Assert.Equal("*", q["d64Program"]);
        Assert.False(q.ContainsKey("diskMount"));
        Assert.Equal("1", q["runLoadedProgram"]);
    }

    [Fact]
    public void Build_DownloadProgram_D64_NoDirectLoad_Emits_DiskMount()
    {
        var request = new C64ShareLinkRequest
        {
            Mode = C64ShareMode.DownloadProgram,
            SystemVariant = "C64NTSC",
            DownloadUrl = "https://example.com/game.d64",
            DownloadType = C64DownloadProgramType.D64,
            DirectLoadPRGName = null,
        };

        var q = ParseQuery(C64ShareLinkBuilder.Build(BaseUrl, request));

        Assert.Equal("1", q["diskMount"]);
        Assert.False(q.ContainsKey("d64Program"));
    }

    [Fact]
    public void Build_CartridgeImage_Emits_LoadCrtUrl_Without_WaitForSystemReady()
    {
        var request = new C64ShareLinkRequest
        {
            Mode = C64ShareMode.CartridgeImage,
            SystemVariant = "C64PAL",
            CartridgeUrl = "https://example.com/fc3.crt",
        };

        var q = ParseQuery(C64ShareLinkBuilder.Build(BaseUrl, request));

        Assert.Equal("C64", q["system"]);
        Assert.Equal("C64PAL", q["systemVariant"]);
        Assert.Equal("1", q["start"]);
        Assert.Equal("https://example.com/fc3.crt", q["loadCrtUrl"]);
        Assert.False(q.ContainsKey("waitForSystemReady"));
        Assert.False(q.ContainsKey("runLoadedProgram"));
        Assert.False(q.ContainsKey("loadPrgUrl"));
        Assert.False(q.ContainsKey("loadD64Url"));
    }

    [Fact]
    public void Build_With_IncludeSettings_Enabled_Emits_All_Runtime_Knobs()
    {
        var request = new C64ShareLinkRequest
        {
            Mode = C64ShareMode.CurrentBasic,
            SystemVariant = "C64NTSC",
            BasicText = "10 print 1\n",
            IncludeSettings = true,
            AudioEnabled = true,
            KeyboardJoystickEnabled = true,
            KeyboardJoystickNumber = 2,
        };

        var q = ParseQuery(C64ShareLinkBuilder.Build(BaseUrl, request));

        Assert.Equal("1", q["audioEnabled"]);
        Assert.Equal("1", q["keyboardJoystickEnabled"]);
        Assert.Equal("2", q["keyboardJoystickNumber"]);
    }

    [Fact]
    public void Build_With_IncludeSettings_And_KeyboardJoystickDisabled_Omits_Number()
    {
        var request = new C64ShareLinkRequest
        {
            Mode = C64ShareMode.CurrentBasic,
            SystemVariant = "C64NTSC",
            BasicText = "10 print 1\n",
            IncludeSettings = true,
            AudioEnabled = false,
            KeyboardJoystickEnabled = false,
            KeyboardJoystickNumber = 2,
        };

        var q = ParseQuery(C64ShareLinkBuilder.Build(BaseUrl, request));

        Assert.Equal("0", q["audioEnabled"]);
        Assert.Equal("0", q["keyboardJoystickEnabled"]);
        Assert.False(q.ContainsKey("keyboardJoystickNumber"));
    }

    [Fact]
    public void Build_Without_IncludeSettings_Omits_Runtime_Knobs()
    {
        var request = new C64ShareLinkRequest
        {
            Mode = C64ShareMode.CurrentBasic,
            SystemVariant = "C64NTSC",
            BasicText = "10 print 1\n",
            IncludeSettings = false,
            AudioEnabled = true,
            KeyboardJoystickEnabled = true,
        };

        var q = ParseQuery(C64ShareLinkBuilder.Build(BaseUrl, request));

        Assert.False(q.ContainsKey("audioEnabled"));
        Assert.False(q.ContainsKey("keyboardJoystickEnabled"));
        Assert.False(q.ContainsKey("keyboardJoystickNumber"));
    }

    [Fact]
    public void Build_CurrentBasic_Without_BasicText_Throws()
    {
        var request = new C64ShareLinkRequest
        {
            Mode = C64ShareMode.CurrentBasic,
            SystemVariant = "C64NTSC",
        };

        Assert.Throws<ArgumentException>(() => C64ShareLinkBuilder.Build(BaseUrl, request));
    }

    [Fact]
    public void Build_DownloadProgram_Without_Url_Throws()
    {
        var request = new C64ShareLinkRequest
        {
            Mode = C64ShareMode.DownloadProgram,
            SystemVariant = "C64NTSC",
            DownloadType = C64DownloadProgramType.Prg,
        };

        Assert.Throws<ArgumentException>(() => C64ShareLinkBuilder.Build(BaseUrl, request));
    }

    [Fact]
    public void Build_CartridgeImage_Without_Url_Throws()
    {
        var request = new C64ShareLinkRequest
        {
            Mode = C64ShareMode.CartridgeImage,
            SystemVariant = "C64NTSC",
        };

        Assert.Throws<ArgumentException>(() => C64ShareLinkBuilder.Build(BaseUrl, request));
    }
}
