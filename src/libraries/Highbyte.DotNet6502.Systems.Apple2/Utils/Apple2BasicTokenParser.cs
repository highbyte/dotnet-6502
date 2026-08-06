using System.Text;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Apple2.Utils;

/// <summary>
/// Parser for a tokenized Applesoft BASIC program (the in-memory/DOS 3.3 "A"-file layout,
/// without any length header): a chain of lines, each
/// [2-byte next-line link][2-byte line number][tokens and literal characters][$00],
/// terminated by a $0000 link.
///
/// Tokens ($80-$EA) are printed with surrounding spaces, the way Applesoft's LIST does;
/// characters inside string literals are copied verbatim. After a REM or DATA token the rest of
/// the line is stored untokenized, so it needs no special handling here. The produced text
/// re-tokenizes to the same program (Applesoft ignores spaces outside strings).
/// </summary>
public class Apple2BasicTokenParser
{
    private readonly ILogger _logger;
    private readonly Apple2System _apple2;

    public Apple2BasicTokenParser(Apple2System apple2, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(nameof(Apple2BasicTokenParser));
        _apple2 = apple2;
    }

    /// <summary>Detokenizes the BASIC program currently in emulated memory.</summary>
    public string GetBasicText(bool spaceAfterLineNumber = true, bool addNewLineAfterLastCharacter = true)
    {
        var startAddress = Apple2System.BASIC_LOAD_ADDRESS;
        var endAddress = _apple2.GetBasicProgramEndAddress();
        if (endAddress <= startAddress)
            return string.Empty;

        var tokenizedBytes = BinarySaver.BuildSaveData(_apple2.Mem, startAddress, endAddress, addFileHeaderWithLoadAddress: false);
        return GetBasicText(tokenizedBytes, spaceAfterLineNumber, addNewLineAfterLastCharacter);
    }

    /// <summary>Detokenizes a tokenized Applesoft program (no header, as stored at $0801).</summary>
    public string GetBasicText(byte[] tokenizedBasic, bool spaceAfterLineNumber = true, bool addNewLineAfterLastCharacter = true)
    {
        using var stream = new MemoryStream(tokenizedBasic);
        var sb = new StringBuilder();

        while (true)
        {
            // Next-line link address; $0000 marks the end of the program.
            var linkAddress = stream.FetchWord();
            if (linkAddress <= 0)
                break;

            var lineNumber = stream.FetchWord();
            if (lineNumber < 0)
                break;

            if (sb.Length > 0)
                sb.AppendLine();

            sb.Append(lineNumber);
            if (spaceAfterLineNumber)
                sb.Append(' ');

            var quoted = false;
            var endOfProgram = false;
            var keywordSpaceEndsLine = false;
            while (true)
            {
                var b = stream.ReadByte();
                if (b < 0)
                {
                    endOfProgram = true;
                    break;
                }
                if (b == 0)
                    break;   // end of line

                if (b == '"')
                    quoted = !quoted;

                if (!quoted && b >= 0x80)
                {
                    if (Apple2BasicTokens.Tokens.TryGetValue((byte)b, out var keyword))
                    {
                        // LIST-style: a space on each side of a keyword, without doubling up.
                        if (sb.Length > 0 && sb[^1] != ' ')
                            sb.Append(' ');
                        sb.Append(keyword);
                        sb.Append(' ');
                        keywordSpaceEndsLine = true;
                    }
                    else
                    {
                        _logger.LogWarning("Invalid Applesoft token ${Token:X2}; skipping.", b);
                    }
                }
                else
                {
                    sb.Append((char)b);
                    keywordSpaceEndsLine = false;
                }
            }

            // Drop the padding space when a keyword was the last thing on the line (literal
            // trailing spaces, e.g. inside REM text, are kept).
            if (keywordSpaceEndsLine && sb.Length > 0 && sb[^1] == ' ')
                sb.Length--;

            if (endOfProgram)
                break;
        }

        if (sb.Length > 0 && addNewLineAfterLastCharacter)
            sb.AppendLine();

        return sb.ToString();
    }
}
