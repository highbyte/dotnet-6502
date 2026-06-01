using Highbyte.DotNet6502.Systems.Vic20.Config;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Vic20;

public class Vic20BasicMemoryTests
{
    [Fact]
    public void GetBasicProgramEndAddress_ReturnsTheLastLoadedBasicByte()
    {
        var vic20 = new Highbyte.DotNet6502.Systems.Vic20.Vic20(new Vic20Config(), NullLoggerFactory.Instance);
        const ushort loadedAtAddress = Highbyte.DotNet6502.Systems.Vic20.Vic20.BASIC_LOAD_ADDRESS;
        const int fileLength = 0x20;

        vic20.InitBasicMemoryVariables(loadedAtAddress, fileLength);

        Assert.Equal((ushort)(loadedAtAddress + fileLength), vic20.GetBasicProgramEndAddress());
        Assert.Equal((ushort)(loadedAtAddress + fileLength + 1), vic20.Mem.FetchWord(0x2d));
        Assert.Equal((ushort)(loadedAtAddress + fileLength + 1), vic20.Mem.FetchWord(0x2f));
        Assert.Equal((ushort)(loadedAtAddress + fileLength + 1), vic20.Mem.FetchWord(0x31));
    }
}
