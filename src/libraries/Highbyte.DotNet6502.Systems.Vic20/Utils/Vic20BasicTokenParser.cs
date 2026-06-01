using System.Text;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Vic20.Utils;

/// <summary>
/// Parser for VIC-20 BASIC V2 programs in binary (token) format.
/// </summary>
public class Vic20BasicTokenParser
{
    private readonly ILogger _logger;
    private readonly Vic20 _vic20;

    public Vic20BasicTokenParser(Vic20 vic20, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(nameof(Vic20BasicTokenParser));
        _vic20 = vic20;
    }

    public string GetBasicText(bool spaceAfterLineNumber = true, bool addNewLineAfterLastCharacter = true)
    {
        ushort startAddressValue = Vic20.BASIC_LOAD_ADDRESS;
        var endAddressValue = _vic20.GetBasicProgramEndAddress();
        var prgBytes = BinarySaver.BuildSaveData(_vic20.Mem, startAddressValue, endAddressValue, addFileHeaderWithLoadAddress: true);

        return GetBasicText(prgBytes, spaceAfterLineNumber, addNewLineAfterLastCharacter);
    }

    public string GetBasicText(byte[] basicPrg, bool spaceAfterLineNumber = true, bool addNewLineAfterLastCharacter = true)
    {
        if (basicPrg.Length < 2)
            throw new ArgumentException("Basic program is too short to contain a load address.");

        using MemoryStream stream = new(basicPrg);

        byte[] prgHeader = new byte[2];
        stream.ReadExactly(prgHeader, 0, 2);
        var loadAddress = ByteHelpers.ToLittleEndianWord(prgHeader[0], prgHeader[1]);

        _logger.LogInformation("Basic load address: {LoadAddress}", loadAddress);
        if (loadAddress != Vic20.BASIC_LOAD_ADDRESS)
        {
            _logger.LogWarning("Basic load address is not the expected {ExpectedLoadAddress}, probably not a BASIC file. Skipping parsing.", Vic20.BASIC_LOAD_ADDRESS);
            return string.Empty;
        }

        StringBuilder sb = new();

        while (true)
        {
            var addr = stream.FetchWord();
            if (addr < 0 || addr == 0)
                break;

            var lineNumber = stream.FetchWord();
            if (lineNumber < 0)
                break;

            if (sb.Length > 0)
                sb.AppendLine();

            sb.Append(lineNumber);
            if (spaceAfterLineNumber)
                sb.Append(' ');

            bool quoted = false;
            bool endOfProgram = false;
            bool nextLine = false;
            while (!endOfProgram || !nextLine)
            {
                var token = stream.ReadByte();
                if (token < 0)
                {
                    endOfProgram = true;
                    break;
                }
                if (token == 0)
                {
                    nextLine = true;
                    break;
                }

                if (token == '"')
                    quoted = !quoted;

                if (!quoted && token >= 0x80)
                    sb.Append(Vic20BasicTokens.Tokens[(byte)token]);
                else
                    sb.Append((char)token);
            }

            if (endOfProgram)
                break;
        }

        if (sb.Length > 0 && addNewLineAfterLastCharacter)
            sb.AppendLine();

        return sb.ToString();
    }
}
