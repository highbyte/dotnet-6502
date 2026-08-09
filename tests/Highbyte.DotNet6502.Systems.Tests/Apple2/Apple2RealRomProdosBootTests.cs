using System.Text;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// The reason the language card exists: ProDOS 8 needs 64 KB, and a 48 KB machine gets exactly as
/// far as ProDOS's own memory check before stopping with <c>RELOCATION/  CONFIGURATION ERROR</c>.
/// This is the gate — that ProDOS boots at all — not that any particular title runs.
///
/// <para>
/// Opt-in, like the DOS 3.3 boot tests: it needs the genuine ROMs and a ProDOS disk image, none of
/// which can be checked in. Point <c>DOTNET6502_APPLE2_PRODOS_DSK</c> at a DOS-sector-ordered
/// ProDOS <c>.dsk</c>. Developed against the 1991 release of Dangerous Dave
/// (<c>a2_Dangerous_Dave_1</c> on archive.org), whose sector order the drive already supports.
/// </para>
/// </summary>
[Trait("TestType", "Integration")]
public class Apple2RealRomProdosBootTests
{
    /// <summary>
    /// Frames to allow ProDOS to reach its prompt. Generous — the prompt is watched for, so this
    /// only bounds a boot that never arrives. Observed at ~600 frames.
    /// </summary>
    private const int MaxFramesToPrompt = 3000;

    /// <summary>Frames to allow the booted disk's title to reach its own graphics.</summary>
    private const int MaxFramesToTitle = 6000;

    private readonly ITestOutputHelper _output;

    public Apple2RealRomProdosBootTests(ITestOutputHelper output) => _output = output;

    [RequiresApple2ProdosBootFact]
    public void Prodos_Boots_Past_The_Memory_Check_And_Runs_From_The_Language_Card()
    {
        var apple2 = BuildMachineWithProdosDisk();
        apple2.Reset();

        // Run until BASIC.SYSTEM puts up its prompt, capturing the machine's state at that moment
        // rather than at the end of the run: what happens after ProDOS hands over is the title's
        // business, and this test is about ProDOS.
        var promptFrame = -1;
        var readRamAtPrompt = false;
        var cardBytesAtPrompt = 0;

        for (var frame = 0; frame < MaxFramesToPrompt; frame++)
        {
            apple2.ExecuteOneFrame();

            if (ScreenContainsBasicPrompt(apple2))
            {
                promptFrame = frame + 1;
                readRamAtPrompt = apple2.LanguageCard.ReadRam;
                cardBytesAtPrompt = CountNonZero(apple2.LanguageCard.Ram);
                break;
            }
        }

        var screenText = string.Join('\n', ReadScreen(apple2));
        _output.WriteLine(screenText);
        _output.WriteLine($"Prompt at frame {promptFrame}; card readRam={readRamAtPrompt} nonZeroBytes={cardBytesAtPrompt}");
        _output.WriteLine($"Disk reads={apple2.DiskController.DataReadCount}, track={apple2.DiskController.CurrentTrack}");

        // The failure this whole feature exists to remove. A 48 KB machine stops here.
        Assert.DoesNotContain("RELOCATION", screenText, StringComparison.Ordinal);

        Assert.True(promptFrame > 0, $"ProDOS did not reach its prompt within {MaxFramesToPrompt} frames.");

        // ProDOS relocates itself into the card and runs from there, so by the time its prompt is up
        // the card holds several KB and is switched in. Both would be false without a card.
        Assert.True(cardBytesAtPrompt > 4096, "Expected ProDOS to have loaded itself into the language card.");
        Assert.True(readRamAtPrompt, "Expected ProDOS to be running with the card switched in.");
    }

    /// <summary>
    /// The milestone: the disk's title actually runs. Developed against "Dangerous Dave in the
    /// Deserted Pirate's Hideout", which was built for a 64 KB Apple II Plus — so unlike the 1991
    /// re-release (which is 65C02 code) it is within this machine's reach.
    ///
    /// <para>Asserted through what the machine does rather than through pixels: it leaves text mode
    /// for hi-res, keeps running without halting, and needs no opcode this CPU lacks.</para>
    /// </summary>
    [RequiresApple2ProdosBootFact]
    public void The_Title_On_The_Disk_Starts_And_Keeps_Running()
    {
        var apple2 = BuildMachineWithProdosDisk();
        apple2.Reset();

        var reachedHiRes = false;
        for (var frame = 0; frame < MaxFramesToTitle; frame++)
        {
            apple2.ExecuteOneFrame();

            if (!apple2.SoftSwitches.TextMode && apple2.SoftSwitches.HiRes)
            {
                reachedHiRes = true;
                break;
            }
        }

        Assert.True(reachedHiRes, $"The title did not switch to hi-res within {MaxFramesToTitle} frames.");

        // Then keep going: a title that draws one frame and jams is not running.
        var hiResBefore = CountNonZero(ReadHiResPage(apple2));
        for (var frame = 0; frame < 600; frame++)
            apple2.ExecuteOneFrame();

        _output.WriteLine(
            $"pc={apple2.CPU.PC:X4} halted={apple2.CPU.IsHalted} hires={apple2.SoftSwitches.HiRes} " +
            $"unknownOpcodes={apple2.CPU.ExecState.UnknownOpCodeCount} " +
            $"hiResNonZero={hiResBefore} -> {CountNonZero(ReadHiResPage(apple2))}");

        Assert.False(apple2.CPU.IsHalted, "The title halted the CPU (a JAM opcode) instead of running.");
        Assert.False(apple2.SoftSwitches.TextMode, "The title dropped back to text mode.");
        Assert.Equal(0UL, apple2.CPU.ExecState.UnknownOpCodeCount);
    }

    /// <summary>Copy of hi-res page 1, used to show the screen is being drawn rather than static.</summary>
    private static byte[] ReadHiResPage(Apple2System apple2)
    {
        var page = new byte[0x2000];
        for (var i = 0; i < page.Length; i++)
            page[i] = apple2.Mem[(ushort)(0x2000 + i)];
        return page;
    }

    private Apple2System BuildMachineWithProdosDisk()
    {
        var romPath = Apple2TestRoms.ResolveSystemRomPath()!;
        var disk2RomPath = Apple2TestRoms.ResolveDisk2RomPath()!;
        var dskPath = Apple2TestRoms.ResolveProdosDskPath()!;

        _output.WriteLine($"System ROM: {romPath}");
        _output.WriteLine($"Disk II boot ROM: {disk2RomPath}");
        _output.WriteLine($"ProDOS disk: {dskPath}");

        var romData = new Dictionary<string, byte[]>
        {
            { Apple2SystemConfig.SYSTEM_ROM_NAME, File.ReadAllBytes(romPath) },
            { Apple2SystemConfig.DISK2_ROM_NAME, File.ReadAllBytes(disk2RomPath) },
        };

        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance, romData);
        apple2.DiskController.InsertDiskImage(File.ReadAllBytes(dskPath));
        return apple2;
    }

    /// <summary>
    /// True once a line starts with the Applesoft prompt, which under ProDOS means BASIC.SYSTEM has
    /// loaded and handed over — that is, ProDOS booted.
    /// </summary>
    private static bool ScreenContainsBasicPrompt(Apple2System apple2)
        => ReadScreen(apple2).Any(row => row.StartsWith(']'));

    private static int CountNonZero(byte[] data)
    {
        var count = 0;
        foreach (var value in data)
        {
            if (value != 0)
                count++;
        }
        return count;
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
