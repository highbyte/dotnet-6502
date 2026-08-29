using System.Text;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Oric.Utils;

/// <summary>
/// Detokenizes an Oric Extended BASIC 1.1 program. Each line is stored as
/// [next-line address][line number][tokens and literal characters][$00], and a $0000
/// next-line address terminates the program.
/// </summary>
public sealed class OricBasicTokenParser
{
    private readonly ILogger _logger;
    private readonly OricMachine _oric;

    public OricBasicTokenParser(OricMachine oric, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(nameof(OricBasicTokenParser));
        _oric = oric;
    }

    /// <summary>Detokenizes the BASIC program currently in emulated memory.</summary>
    public string GetBasicText(bool spaceAfterLineNumber = true, bool addNewLineAfterLastCharacter = true)
    {
        var startAddress = _oric.GetBasicProgramStartAddress();
        var endAddress = _oric.GetBasicProgramEndAddress();
        if (startAddress < OricMachine.BasicProgramDefaultStartAddress ||
            endAddress <= startAddress ||
            endAddress > OricMachine.SystemRomStartAddress)
        {
            return string.Empty;
        }

        var tokenizedBytes = BinarySaver.BuildSaveData(
            _oric.Mem,
            startAddress,
            endAddress,
            addFileHeaderWithLoadAddress: false);
        return GetBasicText(tokenizedBytes, startAddress, spaceAfterLineNumber, addNewLineAfterLastCharacter);
    }

    /// <summary>Detokenizes bytes in the same format in which they are stored in Oric RAM.</summary>
    public string GetBasicText(
        byte[] tokenizedBasic,
        ushort loadAddress = OricMachine.BasicProgramDefaultStartAddress,
        bool spaceAfterLineNumber = true,
        bool addNewLineAfterLastCharacter = true)
    {
        using var stream = new MemoryStream(tokenizedBasic);
        var source = new StringBuilder();

        while (stream.Position + 1 < stream.Length)
        {
            var lineOffset = stream.Position;
            var nextLineAddress = stream.FetchWord();
            if (nextLineAddress == 0)
                break;
            if (nextLineAddress < 0 || stream.Position + 1 >= stream.Length)
            {
                _logger.LogWarning("Truncated Oric BASIC line link or line number.");
                break;
            }

            var currentAddress = loadAddress + lineOffset;
            var endAddress = loadAddress + stream.Length;
            if (nextLineAddress <= currentAddress + 4 || nextLineAddress > endAddress - 2)
            {
                _logger.LogWarning(
                    "Invalid Oric BASIC next-line address ${NextLineAddress:X4} at ${CurrentAddress:X4}.",
                    nextLineAddress,
                    currentAddress);
                break;
            }

            var lineNumber = stream.FetchWord();
            if (lineNumber < 0)
                break;

            if (source.Length > 0)
                source.AppendLine();
            source.Append(lineNumber);
            if (spaceAfterLineNumber)
                source.Append(' ');

            var foundLineEnd = false;
            while (stream.Position < stream.Length)
            {
                var value = stream.ReadByte();
                if (value == 0)
                {
                    foundLineEnd = true;
                    break;
                }

                if (value < 0x80)
                {
                    source.Append((char)value);
                }
                else if (OricBasicTokens.Tokens.TryGetValue((byte)value, out var keyword))
                {
                    source.Append(keyword);
                }
                else
                {
                    _logger.LogWarning("Invalid Oric BASIC token ${Token:X2}; skipping.", value);
                }
            }

            if (!foundLineEnd)
            {
                _logger.LogWarning("Truncated Oric BASIC line {LineNumber}.", lineNumber);
                break;
            }

            var actualNextLineAddress = loadAddress + stream.Position;
            if (actualNextLineAddress != nextLineAddress)
            {
                _logger.LogWarning(
                    "Oric BASIC line {LineNumber} links to ${LinkedAddress:X4}, but its content ends at ${ActualAddress:X4}.",
                    lineNumber,
                    nextLineAddress,
                    actualNextLineAddress);
                break;
            }
        }

        if (source.Length > 0 && addNewLineAfterLastCharacter)
            source.AppendLine();

        return source.ToString();
    }
}
