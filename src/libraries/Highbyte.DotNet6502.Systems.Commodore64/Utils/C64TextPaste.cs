using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.Utils;
public class C64TextPaste
{
    private readonly Queue<char> _charQueue = new();
    private readonly ILogger<C64TextPaste> _logger;
    private readonly C64 _c64;

    internal bool HasCharactersPending => _charQueue.Count > 0;


    public C64TextPaste(C64 c64, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<C64TextPaste>();
        _c64 = c64;
    }

    public void Paste(string text)
    {
        foreach (char c in text)
            _charQueue.Enqueue(c);
    }

    internal void InsertNextCharacterToKeyboardBuffer()
    {
        bool foundChar = _charQueue.TryPeek(out char ansiChar);
        if (!foundChar)
            return;

        // In Windows, a new line is CRLF (Carrige Return 13 and Line Feed 10)
        // In Linux and macOS, a new line is only LF (Line feed 10).
        // C64 only uses LF (13) which is "Return" for new line.
        //
        // Ignore Windows LF (10), and map Line Feed for all systems (10) to C64 Return (13).
        if (ansiChar == 13)
        {
            _charQueue.Dequeue();
            return;
        }

        if (ansiChar == 10)
            ansiChar = (char)13;

        if (!Petscii.CharToPetscii.ContainsKey(ansiChar))
        {
            _charQueue.Dequeue();
            _logger.LogWarning($"'{ansiChar}' has no mapped PetscII char.");
            return;
        }

        var petsciiChar = Petscii.CharToPetscii[ansiChar];
        var inserted = _c64.Cia1.Keyboard.InsertPetsciiCharIntoBuffer(petsciiChar);
        if (inserted)
        {
            _charQueue.Dequeue();
        }
        else
        {
            _logger.LogWarning($"'{ansiChar}' could not be inserted into keyboard buffer.");
        }
    }
}
