using Highbyte.DotNet6502.Systems.Vic20.Config;
using Highbyte.DotNet6502.Systems.Vic20.Utils;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Vic20;

public class Vic20BasicTokenParserTests
{
    private readonly NullLoggerFactory _loggerFactory = new();
    private const string HelloWorldBasicPrgFile = "../../../../../samples/Basic/VIC20/Text/Build/HelloWorld.prg";
    private const string HelloWorldBasicSourceFile = "../../../../../samples/Basic/VIC20/Text/HelloWorld.txt";

    [Fact]
    public void GetBasicText_WhenBasicProgramIsTooShort_ThrowsException()
    {
        var vic20 = BuildVic20();
        var basicTokenParser = new Vic20BasicTokenParser(vic20, _loggerFactory);

        Assert.Throws<ArgumentException>(() => basicTokenParser.GetBasicText(new byte[] { 0x00 }));
    }

    [Fact]
    public void GetBasicText_WhenBasicProgramHasNoLines_ReturnsEmptyString()
    {
        var vic20 = BuildVic20();
        var basicTokenParser = new Vic20BasicTokenParser(vic20, _loggerFactory);

        var sourceCode = basicTokenParser.GetBasicText(new byte[] { 0x00, 0x00 });

        Assert.Empty(sourceCode);
    }

    [Fact]
    public void GetBasicText_Returns_All_Lines_In_Program_ByteArray()
    {
        var vic20 = BuildVic20();
        var basicTokenParser = new Vic20BasicTokenParser(vic20, _loggerFactory);

        var prg = File.ReadAllBytes(HelloWorldBasicPrgFile);
        var sourceCode = basicTokenParser.GetBasicText(prg, addNewLineAfterLastCharacter: false);

        Assert.Equal(ExpectedHelloWorldBasicSourceCode, sourceCode.ToLower());
    }

    [Fact]
    public void GetBasicText_Returns_All_Lines_In_Basic_Program_Loaded_Into_Vic20()
    {
        var vic20 = BuildVic20();
        var basicTokenParser = new Vic20BasicTokenParser(vic20, _loggerFactory);

        BinaryLoader.Load(
            vic20.Mem,
            HelloWorldBasicPrgFile,
            out ushort loadedAtAddress,
            out ushort fileLength);

        vic20.InitBasicMemoryVariables(loadedAtAddress, fileLength);

        var sourceCode = basicTokenParser.GetBasicText(addNewLineAfterLastCharacter: false);

        Assert.Equal(ExpectedHelloWorldBasicSourceCode, sourceCode.ToLower());
    }

    private static string ExpectedHelloWorldBasicSourceCode => File.ReadAllText(HelloWorldBasicSourceFile).TrimEnd();

    private Highbyte.DotNet6502.Systems.Vic20.Vic20 BuildVic20()
    {
        return new Highbyte.DotNet6502.Systems.Vic20.Vic20(new Vic20Config(), _loggerFactory);
    }
}
