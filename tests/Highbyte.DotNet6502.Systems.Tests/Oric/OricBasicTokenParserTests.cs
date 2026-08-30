using Highbyte.DotNet6502.Systems.Oric.Utils;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class OricBasicTokenParserTests
{
    private static OricBasicTokenParser BuildParser(OricMachine oric)
        => new(oric, NullLoggerFactory.Instance);

    private static IEnumerable<byte> Line(ushort address, ushort lineNumber, params byte[] content)
    {
        var nextLineAddress = (ushort)(address + 4 + content.Length + 1);
        yield return (byte)nextLineAddress;
        yield return (byte)(nextLineAddress >> 8);
        yield return (byte)lineNumber;
        yield return (byte)(lineNumber >> 8);
        foreach (var value in content)
            yield return value;
        yield return 0;
    }

    private static byte[] Program(params IEnumerable<byte>[] lines)
    {
        var bytes = lines.SelectMany(line => line).ToList();
        bytes.Add(0);
        bytes.Add(0);
        return bytes.ToArray();
    }

    private static byte[] Chars(string text) => text.Select(character => (byte)character).ToArray();

    [Fact]
    public void TokenTableCoversAtmosBasic11Range()
    {
        Assert.Equal(119, OricBasicTokens.Tokens.Count);
        Assert.Equal("END", OricBasicTokens.Tokens[0x80]);
        Assert.Equal("STORE", OricBasicTokens.Tokens[0x82]);
        Assert.Equal("PRINT", OricBasicTokens.Tokens[0xba]);
        Assert.Equal("MID$", OricBasicTokens.Tokens[0xf6]);
    }

    [Fact]
    public void PrintStatementDetokenizesWithLiteralTextIntact()
    {
        var parser = BuildParser(new OricMachine());
        var content = new byte[] { 0xba }.Concat(Chars("\"Hello, Oric!\"")).ToArray();
        var program = Program(Line(OricMachine.BasicProgramDefaultStartAddress, 10, content));

        var text = parser.GetBasicText(program, addNewLineAfterLastCharacter: false);

        Assert.Equal("10 PRINT\"Hello, Oric!\"", text);
    }

    [Fact]
    public void ConsecutiveKeywordsAndOperatorsUseListStyleWithoutAddedSpaces()
    {
        var parser = BuildParser(new OricMachine());
        // 20 FORI=1TO10:PRINTI:NEXTI
        var content = new byte[]
        {
            0x8d, (byte)'I', 0xd4, (byte)'1', 0xc3, (byte)'1', (byte)'0', (byte)':',
            0xba, (byte)'I', (byte)':', 0x90, (byte)'I',
        };
        var program = Program(Line(OricMachine.BasicProgramDefaultStartAddress, 20, content));

        var text = parser.GetBasicText(program, addNewLineAfterLastCharacter: false);

        Assert.Equal("20 FORI=1TO10:PRINTI:NEXTI", text);
    }

    [Fact]
    public void DataAndRemTextRemainLiteralBecauseTheRomStoresThemUntokenized()
    {
        var parser = BuildParser(new OricMachine());
        var firstContent = new byte[] { 0x91 }.Concat(Chars("PRINT,FOR,lower")).ToArray();
        var secondAddress = (ushort)(OricMachine.BasicProgramDefaultStartAddress + 4 + firstContent.Length + 1);
        var secondContent = new byte[] { 0x9d }.Concat(Chars(" PRINT remains a comment")).ToArray();
        var program = Program(
            Line(OricMachine.BasicProgramDefaultStartAddress, 10, firstContent),
            Line(secondAddress, 20, secondContent));

        var text = parser.GetBasicText(program);

        Assert.Equal(
            $"10 DATAPRINT,FOR,lower{Environment.NewLine}20 REM PRINT remains a comment{Environment.NewLine}",
            text);
    }

    [Fact]
    public void EmptyProgramDetokenizesToEmptyText()
    {
        var parser = BuildParser(new OricMachine());

        Assert.Equal(string.Empty, parser.GetBasicText([0, 0]));
    }

    [Fact]
    public void InvalidLineLinkStopsParsingWithoutWalkingPastTheBuffer()
    {
        var parser = BuildParser(new OricMachine());
        var malformedProgram = new byte[]
        {
            0xff, 0xbf, 10, 0, 0xba, (byte)'1', 0, 0, 0,
        };

        Assert.Equal(string.Empty, parser.GetBasicText(malformedProgram));
    }

    [Fact]
    public void ProgramInEmulatedMemoryUsesAtmosBasicPointers()
    {
        var oric = new OricMachine();
        var parser = BuildParser(oric);
        var content = new byte[] { 0xba }.Concat(Chars("\"HI\"")).ToArray();
        var program = Program(Line(OricMachine.BasicProgramDefaultStartAddress, 10, content));
        for (var index = 0; index < program.Length; index++)
            oric.Mem[(ushort)(OricMachine.BasicProgramDefaultStartAddress + index)] = program[index];
        oric.Mem.WriteWord(OricMachine.BasicProgramStartPointerAddress, OricMachine.BasicProgramDefaultStartAddress);
        oric.Mem.WriteWord(
            OricMachine.BasicProgramEndPointerAddress,
            (ushort)(OricMachine.BasicProgramDefaultStartAddress + program.Length));

        var text = parser.GetBasicText(addNewLineAfterLastCharacter: false);

        Assert.Equal("10 PRINT\"HI\"", text);
    }

    [Fact]
    public void UninitializedMachineYieldsEmptyText()
    {
        var parser = BuildParser(new OricMachine());

        Assert.Equal(string.Empty, parser.GetBasicText());
    }
}
