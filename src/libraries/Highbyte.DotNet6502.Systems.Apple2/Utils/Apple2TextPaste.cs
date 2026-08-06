using Microsoft.Extensions.Logging;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Apple2.Utils;

/// <summary>
/// Types text into the running Apple II by feeding characters through the keyboard latch.
///
/// The machine has no keyboard buffer — just the single $C000 latch with a strobe bit — so
/// pacing is driven by consumption: the next queued character is latched only once software has
/// cleared the strobe of the previous one (the ROM's RDKEY does this for every key it reads).
/// One character is delivered per frame at most, matching how fast a very quick typist looks to
/// the machine.
///
/// Letters are mapped to uppercase (the II Plus keyboard has no lowercase), line endings are
/// normalized to the Apple II RETURN code ($0D), and characters the keyboard cannot produce are
/// dropped with a warning.
/// </summary>
public class Apple2TextPaste
{
    private readonly Queue<char> _charQueue = new();
    private readonly ILogger _logger;
    private readonly Apple2System _apple2;

    internal bool HasCharactersPending => _charQueue.Count > 0;

    public Apple2TextPaste(Apple2System apple2, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(nameof(Apple2TextPaste));
        _apple2 = apple2;
    }

    public void Paste(string text)
    {
        foreach (var c in text)
            _charQueue.Enqueue(c);
    }

    /// <summary>
    /// Latches the next queued character if the previous one has been consumed.
    /// Called once per emulated frame.
    /// </summary>
    internal void InsertNextCharacterToLatch()
    {
        if (!_charQueue.TryPeek(out var ch))
            return;

        // Windows line endings are CRLF; Unix/macOS are LF. The Apple II uses CR ($0D, RETURN).
        // Drop the CR of a CRLF pair and map LF to RETURN, so all conventions land on one $0D.
        if (ch == '\r')
        {
            _charQueue.Dequeue();
            return;
        }
        if (ch == '\n')
            ch = '\r';

        // The keyboard produces 7-bit ASCII, uppercase only.
        ch = char.ToUpperInvariant(ch);
        var ascii = (byte)ch;
        var producible = ch == '\r' || (ascii >= 0x20 && ascii <= 0x5F);
        if (!producible)
        {
            _charQueue.Dequeue();
            _logger.LogWarning("'{Char}' cannot be produced by the Apple II keyboard.", ch);
            return;
        }

        // Wait until the program has consumed the previous key (strobe cleared).
        if (_apple2.Keyboard.StrobeSet)
            return;

        _apple2.Keyboard.KeyPressed(ch == '\r' ? (byte)0x0D : ascii);
        _charQueue.Dequeue();
    }
}
