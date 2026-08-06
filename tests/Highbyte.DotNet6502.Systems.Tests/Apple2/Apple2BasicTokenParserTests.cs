using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

public class Apple2BasicTokenParserTests
{
    private static Apple2System BuildApple2() => new(new Apple2Config(), NullLoggerFactory.Instance);

    private static Apple2BasicTokenParser BuildParser(Apple2System apple2)
        => new(apple2, NullLoggerFactory.Instance);

    /// <summary>Builds one tokenized line: link + line number + content + $00 terminator.</summary>
    private static IEnumerable<byte> Line(ushort linkAddress, ushort lineNumber, params byte[] content)
    {
        yield return (byte)(linkAddress & 0xFF);
        yield return (byte)(linkAddress >> 8);
        yield return (byte)(lineNumber & 0xFF);
        yield return (byte)(lineNumber >> 8);
        foreach (var b in content)
            yield return b;
        yield return 0x00;
    }

    private static byte[] Program(params IEnumerable<byte>[] lines)
    {
        var bytes = new List<byte>();
        foreach (var line in lines)
            bytes.AddRange(line);
        bytes.Add(0x00);   // end-of-program link ($0000)
        bytes.Add(0x00);
        return bytes.ToArray();
    }

    private static byte[] Chars(string text) => text.Select(c => (byte)c).ToArray();

    [Fact]
    public void The_Token_Table_Covers_80_Through_EA()
    {
        Assert.Equal(107, Apple2BasicTokens.Tokens.Count);
        Assert.Equal("END", Apple2BasicTokens.Tokens[0x80]);
        Assert.Equal("HPLOT", Apple2BasicTokens.Tokens[0x93]);
        Assert.Equal("PRINT", Apple2BasicTokens.Tokens[0xBA]);
        Assert.Equal("MID$", Apple2BasicTokens.Tokens[0xEA]);
    }

    [Fact]
    public void A_Print_Statement_Detokenizes_With_The_String_Literal_Intact()
    {
        var parser = BuildParser(BuildApple2());
        var program = Program(
            Line(0x0810, 10, new byte[] { 0xBA }.Concat(Chars("\"HELLO\"")).ToArray()));

        var text = parser.GetBasicText(program, addNewLineAfterLastCharacter: false);

        Assert.Equal("10 PRINT \"HELLO\"", text);
    }

    [Fact]
    public void Operator_Tokens_Get_Spaces_Without_Doubling()
    {
        var parser = BuildParser(BuildApple2());
        // 20 A=A+1
        var program = Program(
            Line(0x0810, 20, (byte)'A', 0xD0, (byte)'A', 0xC8, (byte)'1'));

        var text = parser.GetBasicText(program, addNewLineAfterLastCharacter: false);

        Assert.Equal("20 A = A + 1", text);
    }

    [Fact]
    public void A_Line_Ending_In_A_Keyword_Has_No_Trailing_Space()
    {
        var parser = BuildParser(BuildApple2());
        var program = Program(Line(0x0810, 40, 0x89));   // 40 TEXT

        var text = parser.GetBasicText(program, addNewLineAfterLastCharacter: false);

        Assert.Equal("40 TEXT", text);
    }

    [Fact]
    public void Token_Value_Bytes_Inside_A_String_Stay_Literal()
    {
        var parser = BuildParser(BuildApple2());
        // 10 PRINT "<0xBA>" — the byte that means PRINT outside quotes.
        var program = Program(
            Line(0x0810, 10, 0xBA, (byte)'"', 0xBA, (byte)'"'));

        var text = parser.GetBasicText(program, addNewLineAfterLastCharacter: false);

        Assert.Equal($"10 PRINT \"{(char)0xBA}\"", text);
    }

    [Fact]
    public void Multiple_Lines_Are_Separated_By_Newlines()
    {
        var parser = BuildParser(BuildApple2());
        var program = Program(
            Line(0x0810, 10, new byte[] { 0xBA }.Concat(Chars("\"A\"")).ToArray()),
            Line(0x0820, 20, 0x89));

        var text = parser.GetBasicText(program);

        Assert.Equal($"10 PRINT \"A\"{Environment.NewLine}20 TEXT{Environment.NewLine}", text);
    }

    [Fact]
    public void An_Empty_Program_Detokenizes_To_An_Empty_String()
    {
        var parser = BuildParser(BuildApple2());

        Assert.Equal(string.Empty, parser.GetBasicText(new byte[] { 0x00, 0x00 }));
    }

    [Fact]
    public void The_Program_In_Emulated_Memory_Detokenizes_Via_The_Zero_Page_Pointers()
    {
        var apple2 = BuildApple2();
        var parser = BuildParser(apple2);

        var program = Program(
            Line(0x0810, 10, new byte[] { 0xBA }.Concat(Chars("\"HI\"")).ToArray()));
        for (var i = 0; i < program.Length; i++)
            apple2.Mem[(ushort)(Apple2System.BASIC_LOAD_ADDRESS + i)] = program[i];
        apple2.InitBasicMemoryVariables(Apple2System.BASIC_LOAD_ADDRESS, program.Length);

        var text = parser.GetBasicText(addNewLineAfterLastCharacter: false);

        Assert.Equal("10 PRINT \"HI\"", text);
    }

    [Fact]
    public void An_Uninitialized_Machine_Yields_An_Empty_Source()
    {
        var apple2 = BuildApple2();
        var parser = BuildParser(apple2);

        Assert.Equal(string.Empty, parser.GetBasicText());
    }
}
