using System.Threading;
using System.Threading.Tasks;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Vic20.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;
using Vic20System = Highbyte.DotNet6502.Systems.Vic20.Vic20;

namespace Highbyte.DotNet6502.Systems.Tests;

/// <summary>
/// A halted CPU must not hang the frame loop.
///
/// <para>
/// Each system runs instructions until a cycle budget for the frame is met. A JAM/KIL opcode halts
/// the CPU, and from then on every instruction reports <em>zero</em> cycles — so the budget can
/// never be reached and the loop spins forever. In a host that is the UI thread: the application
/// stops responding rather than merely showing a locked-up emulated machine.
/// </para>
///
/// <para>
/// Found while running a ProDOS title on the Apple II that jumps into unimplemented 65C02 code and
/// eventually executes <c>$B2</c>, which on an NMOS 6502 is JAM. The same gap was present in all
/// three systems; <c>GenericComputer</c> was already safe because it drives the CPU through
/// <c>CPU.Execute</c>, which has always broken out on <c>IsHalted</c>.
/// </para>
///
/// <para>
/// The tests run the frame on a worker thread with a timeout, so a regression fails the run instead
/// of hanging it.
/// </para>
/// </summary>
public class FrameLoopHaltedCpuTests
{
    /// <summary>Generous: a frame is milliseconds of work, so anything near this means "spinning".</summary>
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(20);

    /// <summary>One of the JAM opcodes. Only executable under the FullUnofficial profile.</summary>
    private const byte JamOpcode = 0x02;

    [Fact]
    public void Apple2_Frame_Ends_When_The_Cpu_Halts()
    {
        var apple2 = new Apple2System(
            new Apple2Config { CpuCompatibilityProfile = CpuCompatibilityProfile.FullUnofficial },
            NullLoggerFactory.Instance);

        // Somewhere in the 48 KB of RAM, well clear of zero page and the stack.
        const ushort programAddress = 0x0300;
        apple2.Mem[programAddress] = JamOpcode;
        apple2.CPU.PC = programAddress;

        RunFrameWithTimeout(() => apple2.ExecuteOneFrame(), nameof(Apple2System));

        Assert.True(apple2.CPU.IsHalted, "Precondition: the JAM opcode should have halted the CPU.");
    }

    [Fact]
    public void Vic20_Frame_Ends_When_The_Cpu_Halts()
    {
        var vic20 = new Vic20System(
            new Vic20Config { CpuCompatibilityProfile = CpuCompatibilityProfile.FullUnofficial },
            NullLoggerFactory.Instance);

        const ushort programAddress = 0x1000;   // main RAM on an unexpanded VIC-20
        vic20.Mem[programAddress] = JamOpcode;
        vic20.CPU.PC = programAddress;

        RunFrameWithTimeout(() => vic20.ExecuteOneFrame(), nameof(Vic20System));

        Assert.True(vic20.CPU.IsHalted, "Precondition: the JAM opcode should have halted the CPU.");
    }

    [Fact]
    public void C64_Frame_Ends_When_The_Cpu_Halts()
    {
        var c64 = C64.BuildC64(
            new C64Config
            {
                LoadROMs = false,
                C64Model = "C64PAL",
                Vic2Model = "PAL",
                CpuCompatibilityProfile = CpuCompatibilityProfile.FullUnofficial,
            },
            NullLoggerFactory.Instance);

        const ushort programAddress = 0xC000;   // RAM under no ROM, so the opcode is what executes
        c64.RAM[programAddress] = JamOpcode;
        c64.CPU.PC = programAddress;

        RunFrameWithTimeout(() => c64.ExecuteOneFrame(), nameof(C64));

        Assert.True(c64.CPU.IsHalted, "Precondition: the JAM opcode should have halted the CPU.");
    }

    /// <summary>
    /// Runs one frame on a worker thread and fails if it does not return in time. Without the
    /// timeout a regression would hang the whole test run instead of reporting a failure.
    /// </summary>
    private static void RunFrameWithTimeout(Action executeOneFrame, string systemName)
    {
        var frame = Task.Run(executeOneFrame);

        if (!frame.Wait(FrameTimeout))
            Assert.Fail(
                $"{systemName}.ExecuteOneFrame did not return within {FrameTimeout.TotalSeconds:0} s after the CPU " +
                "halted — the frame loop is spinning on instructions that consume no cycles.");

        // Surfaces any exception the frame threw rather than leaving it observed-but-ignored.
        frame.GetAwaiter().GetResult();
    }
}
