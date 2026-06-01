using Highbyte.DotNet6502.Systems.Vic20.Config;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Vic20;

public class Vic20BinarySampleTests
{
    private const string ColorCyclePrgFile = "../../../../../samples/Assembler/VIC20/Build/color_cycle.prg";

    [Fact]
    public void ColorCycleSample_LoadsAtExpectedAddress_AndUpdatesColorRegister()
    {
        var vic20 = new Highbyte.DotNet6502.Systems.Vic20.Vic20(new Vic20Config(), NullLoggerFactory.Instance);

        BinaryLoader.Load(
            vic20.Mem,
            ColorCyclePrgFile,
            out ushort loadedAtAddress,
            out ushort fileLength);

        Assert.Equal((ushort)0x1200, loadedAtAddress);
        Assert.True(fileLength > 0);

        vic20.CPU.PC = loadedAtAddress;
        vic20.ExecuteOneInstruction(out _);
        vic20.ExecuteOneInstruction(out _);
        vic20.ExecuteOneInstruction(out _);

        Assert.Equal((byte)0x18, vic20.Mem[0x900f]);
    }
}
