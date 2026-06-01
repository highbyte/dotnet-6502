using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Vic20.Utils;

public class Vic20TextPaste
{
    private readonly Queue<char> _charQueue = new();
    private readonly ILogger _logger;
    private readonly Vic20 _vic20;

    internal bool HasCharactersPending => _charQueue.Count > 0;

    public Vic20TextPaste(Vic20 vic20, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(nameof(Vic20TextPaste));
        _vic20 = vic20;
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

        if (ansiChar == 13)
        {
            _charQueue.Dequeue();
            return;
        }

        if (ansiChar == 10)
            ansiChar = (char)13;

        if (!Vic20Petscii.CharToPetscii.ContainsKey(ansiChar))
        {
            _charQueue.Dequeue();
            _logger.LogWarning("'{AnsiChar}' has no mapped PETSCII char.", ansiChar);
            return;
        }

        var petsciiChar = Vic20Petscii.CharToPetscii[ansiChar];
        var inserted = _vic20.Via1.Keyboard.InsertPetsciiCharIntoBuffer(petsciiChar);
        if (inserted)
        {
            _charQueue.Dequeue();
        }
        else
        {
            _logger.LogWarning("'{AnsiChar}' could not be inserted into keyboard buffer.", ansiChar);
        }
    }
}
