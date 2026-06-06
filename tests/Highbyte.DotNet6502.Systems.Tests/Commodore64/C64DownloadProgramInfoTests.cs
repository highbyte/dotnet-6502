using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

public class C64DownloadProgramInfoTests
{
    [Fact]
    public void Defaults_D64_To_Load_Then_Run()
    {
        var programInfo = new C64DownloadProgramInfo(
            "Example D64",
            "https://example.com/example.d64");

        Assert.Equal(new[] { "load\"*\",8,1", "run" }, programInfo.RunCommands);
    }

    [Fact]
    public void Defaults_DirectLoadD64_To_Run_Only()
    {
        var programInfo = new C64DownloadProgramInfo(
            "Example Direct Load D64",
            "https://example.com/example.d64",
            directLoadPRGName: "*");

        Assert.Equal(new[] { "run" }, programInfo.RunCommands);
    }

    [Fact]
    public void Defaults_Prg_To_Run_Only()
    {
        var programInfo = new C64DownloadProgramInfo(
            "Example PRG",
            "https://example.com/example.prg",
            downloadType: C64DownloadProgramType.Prg);

        Assert.Equal(new[] { "run" }, programInfo.RunCommands);
    }
}
