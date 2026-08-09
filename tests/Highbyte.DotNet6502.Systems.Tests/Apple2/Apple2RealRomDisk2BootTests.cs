using System.Text;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// Boots a real DOS 3.3 disk image through the emulated Disk II controller — the Autostart slot
/// scan finds the boot ROM at $C600, the boot ROM reads boot1 off track 0, and DOS loads itself
/// via RWTS reading the nibble streams.
///
/// Like <see cref="Apple2RealRomBootTests"/> these are opt-in, because the system ROM, the
/// Disk II boot ROM, and any real disk image are copyrighted. Without all three they report as
/// <em>skipped</em>, with the reason, rather than silently passing — see
/// <see cref="Apple2TestRoms"/>. They need:
/// <list type="bullet">
/// <item>The system and Disk II ROMs in the Apple II ROM directory under the file names the
/// archive publishes them with (the same names the app's ROM download writes), or the
/// <c>DOTNET6502_APPLE2_ROM</c> / <c>DOTNET6502_APPLE2_DISK2_ROM</c> environment variables.</item>
/// <item>A bootable DOS 3.3 image pointed at by <c>DOTNET6502_APPLE2_BOOT_DSK</c>. There is
/// deliberately no default location: disk images are user content kept wherever the user likes
/// (the host picks them with a file dialog), unlike ROMs which the config manages in a known
/// directory.</item>
/// </list>
/// Run with:
/// <code>DOTNET6502_APPLE2_BOOT_DSK=/path/to/dos33.dsk dotnet test --filter TestType=Integration</code>
/// </summary>
[Trait("TestType", "Integration")]
public class Apple2RealRomDisk2BootTests
{
    /// <summary>
    /// Upper bound on frames to allow for a boot, used only as a safety cap — the boot is waited
    /// for by watching the drive rather than by running a fixed number of frames (see
    /// <see cref="RunUntilBootSettles"/>). A fixed budget was the original approach and proved to be
    /// the wrong shape: adding the language card made DOS 3.3 also load Integer BASIC into it, which
    /// legitimately lengthened the boot and failed a test that was not about boot duration at all.
    /// </summary>
    private const int MaxBootFrames = 12000;

    private readonly ITestOutputHelper _output;

    public Apple2RealRomDisk2BootTests(ITestOutputHelper output) => _output = output;

    [RequiresApple2Disk2BootFact]
    public void Boots_Dos33_From_An_Inserted_Disk_Image()
    {
        var apple2 = BootFromDisk();

        // DOS 3.3 turns the drive off once booted and leaves a prompt on screen.
        var screen = ReadScreen(apple2);
        var screenText = string.Join('\n', screen);
        _output.WriteLine(screenText);

        Assert.True(
            screenText.Contains("DOS VERSION 3.3", StringComparison.Ordinal)
                || screen.Any(row => row.TrimEnd().EndsWith(']')),
            "Expected the DOS 3.3 banner or a BASIC prompt after booting the disk.");
        Assert.False(apple2.DiskController.IsMotorOn, "DOS turns the drive motor off after booting.");
    }

    /// <summary>
    /// The case the host UI actually hits: the machine is already sitting at the BASIC prompt
    /// when the user inserts a disk. A plain reset warm-starts back to the prompt without
    /// scanning the slots, so the power-up byte must be invalidated to force a cold start.
    /// </summary>
    [RequiresApple2Disk2BootFact]
    public void Booting_A_Disk_Works_From_An_Already_Running_Basic_Prompt()
    {
        var apple2 = BootFromDisk(insertDisk: false, runFrames: 200);

        Assert.True(apple2.HasBasicStarted(), "Precondition: the machine reached the BASIC prompt.");

        var dskPath = Apple2TestRoms.ResolveBootDskPath()!;
        apple2.DiskController.InsertDiskImage(File.ReadAllBytes(dskPath));
        apple2.InvalidatePowerUpByte();
        apple2.Reset();

        RunUntilBootSettles(apple2);

        var screenText = string.Join('\n', ReadScreen(apple2));
        _output.WriteLine(screenText);
        Assert.Contains("DOS VERSION 3.3", screenText, StringComparison.Ordinal);
    }

    /// <summary>
    /// The workflow the real machine uses: boot DOS once, then swap diskettes freely. Inserting
    /// must not disturb the running machine — Applesoft itself has no disk commands, so losing
    /// resident DOS would leave the user unable to read the disk they just inserted.
    /// </summary>
    [RequiresApple2Disk2BootFact]
    public void Inserting_A_Disk_Does_Not_Disturb_A_Running_Dos()
    {
        var apple2 = BootFromDisk();

        var screenBefore = string.Join('\n', ReadScreen(apple2));
        Assert.Contains("DOS VERSION 3.3", screenBefore, StringComparison.Ordinal);

        // Swap in another diskette (the same image again is enough to prove the point).
        apple2.DiskController.InsertDiskImage(File.ReadAllBytes(Apple2TestRoms.ResolveBootDskPath()!));
        for (var frame = 0; frame < 60; frame++)
            apple2.ExecuteOneFrame();

        // DOS is still resident and the machine did not restart.
        Assert.Contains("DOS VERSION 3.3", string.Join('\n', ReadScreen(apple2)), StringComparison.Ordinal);
        Assert.True(apple2.DiskController.IsDiskInserted);
    }

    /// <summary>Without the cold-start fix a reset just returns to the BASIC prompt.</summary>
    [RequiresApple2Disk2BootFact]
    public void A_Plain_Reset_From_The_Basic_Prompt_Does_Not_Scan_The_Slots()
    {
        var apple2 = BootFromDisk(insertDisk: false, runFrames: 200);

        var dskPath = Apple2TestRoms.ResolveBootDskPath()!;
        apple2.DiskController.InsertDiskImage(File.ReadAllBytes(dskPath));
        apple2.Reset();   // warm start: no power-up byte invalidation

        for (var frame = 0; frame < 600; frame++)
            apple2.ExecuteOneFrame();

        Assert.DoesNotContain("DOS VERSION 3.3", string.Join('\n', ReadScreen(apple2)), StringComparison.Ordinal);
    }

    [RequiresApple2Disk2BootFact]
    public void Without_A_Disk_The_Machine_Still_Boots_To_Basic()
    {
        var apple2 = BootFromDisk(insertDisk: false);

        Assert.True(apple2.HasBasicStarted(), "The Autostart slot scan must fall through to BASIC.");
    }

    /// <summary>
    /// Callers are guarded by <see cref="RequiresApple2Disk2BootFactAttribute"/>, so missing ROMs
    /// or disk image are a skipped test rather than something to handle here.
    /// </summary>
    private Apple2System BootFromDisk(bool insertDisk = true, int? runFrames = null)
    {
        var romPath = Apple2TestRoms.ResolveSystemRomPath();
        var disk2RomPath = Apple2TestRoms.ResolveDisk2RomPath();
        var dskPath = Apple2TestRoms.ResolveBootDskPath();
        Assert.NotNull(romPath);
        Assert.NotNull(disk2RomPath);
        Assert.NotNull(dskPath);

        _output.WriteLine($"System ROM: {romPath}");
        _output.WriteLine($"Disk II boot ROM: {disk2RomPath}");
        _output.WriteLine($"Boot disk: {dskPath}");

        var romData = new Dictionary<string, byte[]>
        {
            { Apple2SystemConfig.SYSTEM_ROM_NAME, File.ReadAllBytes(romPath) },
            { Apple2SystemConfig.DISK2_ROM_NAME, File.ReadAllBytes(disk2RomPath) },
        };

        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance, romData);

        if (insertDisk)
            apple2.DiskController.InsertDiskImage(File.ReadAllBytes(dskPath));

        // Reset after inserting so the Autostart slot scan sees the controller.
        apple2.Reset();

        if (runFrames.HasValue)
        {
            for (var frame = 0; frame < runFrames.Value; frame++)
                apple2.ExecuteOneFrame();
            return apple2;
        }

        RunUntilBootSettles(apple2);
        return apple2;
    }

    /// <summary>
    /// Runs the machine until the drive has spun up and then stayed idle for a while — the
    /// observable end of a boot — rather than for a fixed number of frames. Returns early on the
    /// safety cap so a boot that never completes still fails on the test's own assertions, with the
    /// frame count reported either way.
    /// </summary>
    private void RunUntilBootSettles(Apple2System apple2)
    {
        // Long enough to span DOS's own pauses between loads (it stops the motor between the DOS
        // image, the greeting program and, with a language card fitted, Integer BASIC).
        const int IdleFramesRequired = 120;

        var motorHasRun = false;
        var idleFrames = 0;

        for (var frame = 0; frame < MaxBootFrames; frame++)
        {
            apple2.ExecuteOneFrame();

            if (apple2.DiskController.IsMotorOn)
            {
                motorHasRun = true;
                idleFrames = 0;
            }
            else if (motorHasRun && ++idleFrames >= IdleFramesRequired)
            {
                _output.WriteLine($"Boot settled after {frame + 1} frames.");
                return;
            }
        }

        _output.WriteLine($"Boot did not settle within the {MaxBootFrames}-frame cap.");
    }



    private static string[] ReadScreen(Apple2System apple2)
    {
        var rows = new string[Apple2TextScreen.Rows];
        for (var row = 0; row < Apple2TextScreen.Rows; row++)
        {
            var sb = new StringBuilder(Apple2TextScreen.Columns);
            for (var col = 0; col < Apple2TextScreen.Columns; col++)
                sb.Append(Apple2CharSet.ScreenCodeToUnicode(apple2.Mem[Apple2TextScreen.GetAddress(row, col)]));
            rows[row] = sb.ToString();
        }
        return rows;
    }
}
