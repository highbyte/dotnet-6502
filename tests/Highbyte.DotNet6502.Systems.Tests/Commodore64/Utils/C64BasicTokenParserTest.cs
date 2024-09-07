using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Utils;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Utils;
public class C64BasicTokenParserTest
{
    private readonly ITestOutputHelper _output;
    private readonly ILoggerFactory _loggerFactory = new NullLoggerFactory();
    private const string HelloWorldBasicPrgFile = "../../../../../samples/Basic/C64/Text/Build/HelloWorld.prg";

    private const string HelloWorldBasicSourceCode =
@"10 c1=7:c2=14
20 c=c1
30 if c=c1 then c=c2 : goto 50
40 if c=c2 then c=c1
50 poke 53280,c
60 print ""hello world!""
70 for i=1 to 150:next
80 goto 30";

    public C64BasicTokenParserTest(ITestOutputHelper testOutputHelper)
    {
        _output = testOutputHelper;
    }

    [Fact]
    public void GetBasicTextLines_WhenBasicProgramIsTooShort_ThrowsException()
    {
        // Arrange
        var c64 = BuildC64();
        var basicTokenParser = new C64BasicTokenParser(c64, _loggerFactory);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => basicTokenParser.GetBasicTextLines(new byte[] { 0x00 }));
    }

    [Fact]
    public void GetBasicTextLines_WhenBasicProgramHasNoLines_ReturnsEmptyList()
    {
        // Arrange
        var c64 = BuildC64();
        var basicTokenParser = new C64BasicTokenParser(c64, _loggerFactory);

        // Act
        var sourceCode = basicTokenParser.GetBasicTextLines(new byte[] { 0x00, 0x00 });

        // Assert
        Assert.Empty(sourceCode);
    }

    [Fact]
    public void GetBasicTextLines_Returns_All_Lines_In_Program_ByteArray()
    {
        // Arrange
        var c64 = BuildC64();
        var basicTokenParser = new C64BasicTokenParser(c64, _loggerFactory);

        // Act
        var prg = File.ReadAllBytes(HelloWorldBasicPrgFile);
        var sourceCode = basicTokenParser.GetBasicTextLines(prg, addNewLineAfterLastCharacter: false);

        // Assert
        _output.WriteLine(sourceCode);

        var lines = sourceCode.Split(Environment.NewLine).ToList();
        Assert.Equal(8, lines.Count);

        Assert.Equal(HelloWorldBasicSourceCode, sourceCode.ToLower());
    }

    [Fact]
    public void GetBasicTextLines_Returns_All_Lines_In_Basic_Program_Loaded_Into_C64()
    {
        // Arrange
        var c64 = BuildC64();
        var basicTokenParser = new C64BasicTokenParser(c64, _loggerFactory);

        BinaryLoader.Load(
            c64.Mem,
            HelloWorldBasicPrgFile,
            out ushort loadedAtAddress,
            out ushort fileLength);

        c64.InitBasicMemoryVariables(loadedAtAddress, fileLength);

        // Act
        var sourceCode = basicTokenParser.GetBasicTextLines(addNewLineAfterLastCharacter: false);

        // Assert
        _output.WriteLine(sourceCode);

        var lines = sourceCode.Split(Environment.NewLine).ToList();
        Assert.Equal(8, lines.Count);

        Assert.Equal(HelloWorldBasicSourceCode, sourceCode.ToLower());
    }


    private C64 BuildC64()
    {
        var c64Config = new C64Config
        {
            C64Model = "C64NTSC",   // C64NTSC, C64PAL
            Vic2Model = "NTSC",     // NTSC, NTSC_old, PAL
            LoadROMs = false
        };

        var c64 = C64.BuildC64(c64Config, _loggerFactory);
        return c64;
    }
}
