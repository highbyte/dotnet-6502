using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// Unit tests for the Applesoft zero-page initialisation used when a tokenized BASIC program is
/// placed in memory by an external loader (monitor commands, UI, Remote Control Server).
/// No ROM is required: only zero-page state is inspected.
/// </summary>
public class Apple2BasicMemoryVariablesTests
{
    private static Apple2System Build() => new(new Apple2Config(), NullLoggerFactory.Instance);

    [Fact]
    public void InitBasicMemoryVariables_Sets_All_Applesoft_Program_Pointers()
    {
        var apple2 = Build();
        const int programLength = 9;    // e.g. a tokenized "10 PRINT 3"

        apple2.InitBasicMemoryVariables(Apple2System.BASIC_LOAD_ADDRESS, programLength);

        ushort expectedVarStart = Apple2System.BASIC_LOAD_ADDRESS + programLength;
        Assert.Equal(Apple2System.BASIC_LOAD_ADDRESS, apple2.Mem.FetchWord(0x67));  // TXTTAB
        Assert.Equal(expectedVarStart, apple2.Mem.FetchWord(0x69));                 // VARTAB
        Assert.Equal(expectedVarStart, apple2.Mem.FetchWord(0x6B));                 // ARYTAB
        Assert.Equal(expectedVarStart, apple2.Mem.FetchWord(0x6D));                 // STREND
        Assert.Equal(expectedVarStart, apple2.Mem.FetchWord(0xAF));                 // PRGEND
    }

    [Fact]
    public void InitBasicMemoryVariables_Zeroes_The_Byte_Before_The_Program()
    {
        var apple2 = Build();
        apple2.Mem[(ushort)(Apple2System.BASIC_LOAD_ADDRESS - 1)] = 0xFF;

        apple2.InitBasicMemoryVariables(Apple2System.BASIC_LOAD_ADDRESS, 9);

        Assert.Equal(0x00, apple2.Mem[(ushort)(Apple2System.BASIC_LOAD_ADDRESS - 1)]);
    }

    /// <summary>
    /// The end address is an exclusive bound (one past the program), which is what
    /// <c>BinarySaver.BuildSaveData</c> and the monitor's save command expect — saving must
    /// produce exactly the bytes that were loaded.
    /// </summary>
    [Fact]
    public void GetBasicProgramEndAddress_Is_An_Exclusive_Bound_That_Round_Trips_A_Save()
    {
        var apple2 = Build();
        const int programLength = 9;

        apple2.InitBasicMemoryVariables(Apple2System.BASIC_LOAD_ADDRESS, programLength);

        var endAddress = apple2.GetBasicProgramEndAddress();
        Assert.Equal((ushort)(Apple2System.BASIC_LOAD_ADDRESS + programLength), endAddress);

        var saveData = BinarySaver.BuildSaveData(
            apple2.Mem,
            Apple2System.BASIC_LOAD_ADDRESS,
            endAddress,
            addFileHeaderWithLoadAddress: false);
        Assert.Equal(programLength, saveData.Length);
    }
}
