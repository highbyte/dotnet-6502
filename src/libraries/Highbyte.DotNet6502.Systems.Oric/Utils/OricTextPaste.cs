using Microsoft.Extensions.Logging;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Oric.Utils;

/// <summary>
/// Types clipboard text into an Atmos by feeding 7-bit ASCII through the ROM keyboard latch.
/// The BASIC ROM consumes and tokenizes the submitted lines exactly as it does physical input.
/// </summary>
public sealed class OricTextPaste
{
    private readonly Queue<char> _charQueue = new();
    private readonly ILogger _logger;
    private readonly OricMachine _oric;

    internal bool HasCharactersPending => _charQueue.Count > 0;

    public OricTextPaste(OricMachine oric, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(nameof(OricTextPaste));
        _oric = oric;
    }

    public void Paste(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '\r')
            {
                _charQueue.Enqueue('\r');
                if (index + 1 < text.Length && text[index + 1] == '\n')
                    index++;
            }
            else
            {
                _charQueue.Enqueue(character == '\n' ? '\r' : character);
            }
        }
    }

    /// <summary>
    /// Latches at most one queued character, waiting until the ROM has consumed the previous one.
    /// Called once per emulated frame.
    /// </summary>
    internal void InsertNextCharacterToLatch()
    {
        if (!_charQueue.TryPeek(out var character))
            return;

        if (character != '\r' && (character < 0x20 || character > 0x7e))
        {
            _charQueue.Dequeue();
            _logger.LogWarning("'{Char}' cannot be produced by the Oric keyboard.", character);
            return;
        }

        if ((_oric.Mem[OricMachine.KeyboardCharacterLatchAddress] & 0x80) != 0)
            return;

        var ascii = character == '\r' ? (byte)0x0d : (byte)character;
        _oric.Mem[OricMachine.KeyboardCharacterLatchAddress] = (byte)(ascii | 0x80);
        _charQueue.Dequeue();
    }

    internal void Reset() => _charQueue.Clear();
}
