using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Peripherals;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// Drives the genuine ROM's PREAD routine against the emulated game port.
///
/// This is the test that matters for paddles. The game port is not read as a value but as a
/// duration, so a hold time that is merely close produces positions that are merely close — games
/// would feel subtly wrong rather than fail, which is the hardest kind of bug to notice. Running
/// the real ROM's counting loop and asserting it recovers the exact position that was set pins the
/// contract end to end, rather than testing our own model against itself.
/// </summary>
[Trait("TestType", "Integration")]
public class Apple2RealRomPaddleReadTests
{
    /// <summary>PREAD in the Apple II Plus monitor ROM: strobe $C070, count until bit 7 drops.</summary>
    private const ushort PreadAddress = 0xFB1E;

    /// <summary>Where PREAD's RTS returns to; arbitrary, just needs to be recognisable.</summary>
    private const ushort ReturnSentinel = 0x9000;

    private readonly ITestOutputHelper _output;

    public Apple2RealRomPaddleReadTests(ITestOutputHelper output) => _output = output;

    private Apple2System BootRealRom()
    {
        var romPath = Apple2TestRoms.ResolveSystemRomPath();
        Assert.NotNull(romPath);

        var romData = new Dictionary<string, byte[]>
        {
            { Apple2SystemConfig.SYSTEM_ROM_NAME, File.ReadAllBytes(romPath) },
        };
        var apple2 = new Apple2System(new Apple2Config(), NullLoggerFactory.Instance, romData);
        for (var frame = 0; frame < 180; frame++)
            apple2.ExecuteOneFrame();
        return apple2;
    }

    /// <summary>
    /// Calls PREAD for one paddle and returns what it counted, exactly as Applesoft's PDL() does.
    /// </summary>
    private static byte CallPread(Apple2System apple2, int paddle)
    {
        var cpu = apple2.CPU;
        var mem = apple2.Mem;

        // Push the sentinel as PREAD's return address (RTS pops address-1 and adds one).
        var returnMinusOne = ReturnSentinel - 1;
        mem[(ushort)(0x0100 + cpu.SP)] = (byte)(returnMinusOne >> 8);
        cpu.SP--;
        mem[(ushort)(0x0100 + cpu.SP)] = (byte)(returnMinusOne & 0xFF);
        cpu.SP--;

        cpu.X = (byte)paddle;
        cpu.PC = PreadAddress;

        // PREAD is bounded (it gives up at 255), so a generous instruction cap only guards
        // against a hang if the timer model never lets the bit drop.
        for (var step = 0; step < 20_000 && cpu.PC != ReturnSentinel; step++)
            cpu.ExecuteOneInstruction(mem);

        Assert.Equal(ReturnSentinel, cpu.PC);
        return cpu.Y;
    }

    [RequiresApple2RomTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(64)]
    [InlineData(127)]
    [InlineData(200)]
    public void Pread_Counts_Back_The_Paddle_Position_That_Was_Set(byte position)
    {
        var apple2 = BootRealRom();
        apple2.GamePort.SetPaddlePosition(0, position);

        var read = CallPread(apple2, paddle: 0);
        _output.WriteLine($"set {position} -> PREAD returned {read}");

        Assert.Equal(position, read);
    }

    [RequiresApple2RomFact]
    public void Pread_Saturates_Rather_Than_Wrapping_At_The_Top_Of_The_Range()
    {
        var apple2 = BootRealRom();
        apple2.GamePort.SetPaddlePosition(0, 255);

        var read = CallPread(apple2, paddle: 0);
        _output.WriteLine($"set 255 -> PREAD returned {read}");

        // PREAD's counter is a byte; the top of the range must not roll back round to 0.
        Assert.True(read >= 254, $"Expected the top of the range, got {read}.");
    }

    [RequiresApple2RomFact]
    public void Each_Paddle_Is_Read_Independently()
    {
        var apple2 = BootRealRom();
        apple2.GamePort.SetPaddlePosition(0, 30);
        apple2.GamePort.SetPaddlePosition(1, 200);

        Assert.Equal(30, CallPread(apple2, paddle: 0));
        Assert.Equal(200, CallPread(apple2, paddle: 1));
    }
}
