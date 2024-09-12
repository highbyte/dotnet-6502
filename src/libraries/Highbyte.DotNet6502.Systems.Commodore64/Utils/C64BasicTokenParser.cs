using System.Text;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.Utils;

/// <summary>
/// Parser for C64 Basic program in binary (token) format.
/// 
/// Based on: https://github.com/abbrev/prg-tools/blob/master/src/prg2bas.c
/// </summary>
public class C64BasicTokenParser
{
    private readonly ILogger<C64BasicTokenParser> _logger;
    private readonly C64 _c64;

    public C64BasicTokenParser(C64 c64, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<C64BasicTokenParser>();
        _c64 = c64;
    }

    public string GetBasicText(bool spaceAfterLineNumber = true, bool addNewLineAfterLastCharacter = true)
    {
        ushort startAddressValue = C64.BASIC_LOAD_ADDRESS;
        var endAddressValue = _c64.GetBasicProgramEndAddress();
        var prgBytes = BinarySaver.BuildSaveData(_c64.Mem, startAddressValue, endAddressValue, addFileHeaderWithLoadAddress: true);

        return GetBasicText(prgBytes, spaceAfterLineNumber, addNewLineAfterLastCharacter);
    }

    public string GetBasicText(byte[] basicPrg, bool spaceAfterLineNumber = true, bool addNewLineAfterLastCharacter = true)
    {
        if (basicPrg.Length < 2)
            throw new ArgumentException("Basic program is too short to contain a load address.");

        using MemoryStream stream = new MemoryStream(basicPrg);

        // First to bytes is the load address
        byte[] prgHeader = new byte[2];
        stream.ReadExactly(prgHeader, 0, 2);
        var loadAddress = ByteHelpers.ToLittleEndianWord(prgHeader[0], prgHeader[1]);

        _logger.LogInformation($"Basic load address: {loadAddress}");
        if (loadAddress != C64.BASIC_LOAD_ADDRESS)
        {
            _logger.LogWarning($"Basic load address is not the expected {C64.BASIC_LOAD_ADDRESS}, probably not a Basic file. Skipping parsing.");
            return string.Empty;
        }

        StringBuilder sb = new StringBuilder();

        // Loop each basic line
        while (true)
        {

            // Get next line address
            var addr = stream.FetchWord();
            if (addr < 0 || addr == 0)   // Negative -> end of stream, 0 -> end of basic program
                break;

            // Get next line number
            var lineNumber = stream.FetchWord();
            if (lineNumber < 0)    // Negative -> end of stream,
                break;

            // Add new line if not first line
            if (sb.Length > 0)
                sb.AppendLine();

            sb.Append(lineNumber);
            if (spaceAfterLineNumber)
                sb.Append(' ');


            // Loop each token in the basic line
            bool quoted = false;
            bool endOfProgram = false;
            bool nextLine = false;
            while (!endOfProgram || !nextLine)
            {
                var token = stream.ReadByte();
                if (token < 0) // Negative -> end of stream
                {
                    endOfProgram = true;
                    break;
                }
                if (token == 0) // Next line
                {
                    nextLine = true;
                    break;
                }

                if (token == '"')
                    quoted = !quoted;

                if (!quoted && token >= 0x80)
                {
                    sb.Append(C64BasicTokens.Tokens[(byte)token]);
                }
                else
                {
                    sb.Append((char)token);
                }
            }

            if (endOfProgram)
                break;
        }

        if (addNewLineAfterLastCharacter)
            sb.AppendLine();

        return sb.ToString();
    }
}
