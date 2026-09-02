using System.Text;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

/// <summary>
/// Boot-to-BASIC verification against the genuine C64 ROMs. This is the first program-level C64
/// oracle: everything the cycle-exact work later validates with VICE test programs runs through
/// the same ROM boot, so this has to pass before those tests mean anything.
///
/// The ROMs are not checked in; see <see cref="C64TestRoms"/> for how they are located or fetched
/// and why the tests skip visibly instead of passing empty.
/// </summary>
[Trait("TestType", "Integration")]
public class C64RealRomBootTests
{
    /// <summary>Upper bound on frames to reach the READY prompt; a real PAL C64 gets there in about 2 s (~100 frames).</summary>
    private const int MaxBootFrames = 400;

    private readonly ITestOutputHelper _output;

    public C64RealRomBootTests(ITestOutputHelper output) => _output = output;

    [RequiresC64RomsFact]
    public void Reset_Vector_Points_Into_The_Kernal()
    {
        var c64 = C64.BuildC64(C64TestRoms.CreateConfig(), NullLoggerFactory.Instance);

        Assert.Equal((ushort)0xFCE2, c64.Mem.FetchWord(0xFFFC));
    }

    [RequiresC64RomsFact]
    public void Boots_To_The_Basic_Ready_Prompt()
    {
        var c64 = C64.BuildC64(C64TestRoms.CreateConfig(), NullLoggerFactory.Instance);

        var frames = 0;
        while (!c64.HasBasicStarted() && frames < MaxBootFrames)
        {
            c64.ExecuteOneFrame();
            frames++;
        }
        _output.WriteLine($"BASIC started after {frames} frames");
        Assert.True(c64.HasBasicStarted(), $"BASIC did not initialise within {MaxBootFrames} frames.");

        // Let the KERNAL finish printing the banner and the prompt.
        for (var i = 0; i < 10; i++)
            c64.ExecuteOneFrame();

        var screen = ReadScreen(c64);
        foreach (var row in screen.Where(r => r.Trim().Length > 0))
            _output.WriteLine(row);

        Assert.Contains(screen, row => row.Contains("COMMODORE 64 BASIC V2", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(screen, row => row.Contains("38911 BASIC BYTES FREE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(screen, row => row.StartsWith("READY.", StringComparison.OrdinalIgnoreCase));
    }

    [RequiresC64RomsFact]
    public void Boot_Is_Deterministic()
    {
        var first = BootAndCountFrames();
        var second = BootAndCountFrames();

        Assert.Equal(first.Frames, second.Frames);
        Assert.Equal(first.Cycles, second.Cycles);
    }

    private static (int Frames, ulong Cycles) BootAndCountFrames()
    {
        var c64 = C64.BuildC64(C64TestRoms.CreateConfig(), NullLoggerFactory.Instance);
        var frames = 0;
        while (!c64.HasBasicStarted() && frames < MaxBootFrames)
        {
            c64.ExecuteOneFrame();
            frames++;
        }
        return (frames, c64.CPU.ExecState.CyclesConsumed);
    }

    /// <summary>The 25 text rows of the default screen at $0400, converted from screen codes to ASCII.</summary>
    private static string[] ReadScreen(C64 c64)
    {
        const ushort screenBase = 0x0400;
        var rows = new string[25];
        for (var row = 0; row < 25; row++)
        {
            var bytes = c64.Mem.ReadData((ushort)(screenBase + row * 40), 40);
            var sb = new StringBuilder(40);
            foreach (var screenCode in bytes)
            {
                var petscii = Petscii.C64ScreenCodeToPetscII(screenCode);
                sb.Append((char)Petscii.PetscIIToAscII(petscii));
            }
            rows[row] = sb.ToString();
        }
        return rows;
    }
}
