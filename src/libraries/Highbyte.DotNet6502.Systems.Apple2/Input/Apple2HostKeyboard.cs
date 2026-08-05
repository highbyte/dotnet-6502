using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Systems.Apple2.Input;

/// <summary>
/// Maps host keys to the 7-bit ASCII codes the Apple II keyboard encoder produces.
///
/// Far simpler than a matrix machine: the encoder emits one character code per key press, so
/// there is no chord resolution to do — only the US-QWERTY shifted/unshifted pairs plus the
/// Control modifier. The Apple II and II Plus have no lowercase, so letters always produce
/// uppercase codes regardless of Shift or Caps Lock.
/// </summary>
public class Apple2HostKeyboard
{
    /// <summary>Host key → (unshifted, shifted) ASCII code.</summary>
    public static readonly IReadOnlyDictionary<HostKey, (byte Plain, byte Shifted)> HostKeyToAsciiMap =
        new Dictionary<HostKey, (byte, byte)>
        {
            // Letters — uppercase in both states (no lowercase character generator).
            { HostKey.KeyA, ((byte)'A', (byte)'A') },
            { HostKey.KeyB, ((byte)'B', (byte)'B') },
            { HostKey.KeyC, ((byte)'C', (byte)'C') },
            { HostKey.KeyD, ((byte)'D', (byte)'D') },
            { HostKey.KeyE, ((byte)'E', (byte)'E') },
            { HostKey.KeyF, ((byte)'F', (byte)'F') },
            { HostKey.KeyG, ((byte)'G', (byte)'G') },
            { HostKey.KeyH, ((byte)'H', (byte)'H') },
            { HostKey.KeyI, ((byte)'I', (byte)'I') },
            { HostKey.KeyJ, ((byte)'J', (byte)'J') },
            { HostKey.KeyK, ((byte)'K', (byte)'K') },
            { HostKey.KeyL, ((byte)'L', (byte)'L') },
            { HostKey.KeyM, ((byte)'M', (byte)'M') },
            { HostKey.KeyN, ((byte)'N', (byte)'N') },
            { HostKey.KeyO, ((byte)'O', (byte)'O') },
            { HostKey.KeyP, ((byte)'P', (byte)'P') },
            { HostKey.KeyQ, ((byte)'Q', (byte)'Q') },
            { HostKey.KeyR, ((byte)'R', (byte)'R') },
            { HostKey.KeyS, ((byte)'S', (byte)'S') },
            { HostKey.KeyT, ((byte)'T', (byte)'T') },
            { HostKey.KeyU, ((byte)'U', (byte)'U') },
            { HostKey.KeyV, ((byte)'V', (byte)'V') },
            { HostKey.KeyW, ((byte)'W', (byte)'W') },
            { HostKey.KeyX, ((byte)'X', (byte)'X') },
            { HostKey.KeyY, ((byte)'Y', (byte)'Y') },
            { HostKey.KeyZ, ((byte)'Z', (byte)'Z') },

            // Digit row — US-QWERTY shifted symbols.
            { HostKey.Digit1, ((byte)'1', (byte)'!') },
            { HostKey.Digit2, ((byte)'2', (byte)'@') },
            { HostKey.Digit3, ((byte)'3', (byte)'#') },
            { HostKey.Digit4, ((byte)'4', (byte)'$') },
            { HostKey.Digit5, ((byte)'5', (byte)'%') },
            { HostKey.Digit6, ((byte)'6', (byte)'^') },
            { HostKey.Digit7, ((byte)'7', (byte)'&') },
            { HostKey.Digit8, ((byte)'8', (byte)'*') },
            { HostKey.Digit9, ((byte)'9', (byte)'(') },
            { HostKey.Digit0, ((byte)'0', (byte)')') },

            // Punctuation.
            { HostKey.Minus, ((byte)'-', (byte)'_') },
            { HostKey.Equal, ((byte)'=', (byte)'+') },
            { HostKey.BracketLeft, ((byte)'[', (byte)'{') },
            { HostKey.BracketRight, ((byte)']', (byte)'}') },
            { HostKey.Backslash, ((byte)'\\', (byte)'|') },
            { HostKey.Semicolon, ((byte)';', (byte)':') },
            { HostKey.Quote, ((byte)'\'', (byte)'"') },
            { HostKey.Comma, ((byte)',', (byte)'<') },
            { HostKey.Period, ((byte)'.', (byte)'>') },
            { HostKey.Slash, ((byte)'/', (byte)'?') },
            { HostKey.Backquote, ((byte)'`', (byte)'~') },

            // Control keys.
            { HostKey.Space, (0x20, 0x20) },
            { HostKey.Enter, (0x0D, 0x0D) },      // RETURN
            { HostKey.Tab, (0x09, 0x09) },
            { HostKey.Escape, (0x1B, 0x1B) },     // ESC
            { HostKey.Backspace, (0x08, 0x08) },  // same code as the left-arrow key
            { HostKey.ArrowLeft, (0x08, 0x08) },
            { HostKey.ArrowRight, (0x15, 0x15) },
            // The II and II Plus have no up/down arrow keys; these produce the codes the
            // Monitor and Applesoft treat as line up / line feed.
            { HostKey.ArrowUp, (0x0B, 0x0B) },
            { HostKey.ArrowDown, (0x0A, 0x0A) },
        };

    /// <summary>Host keys that only act as modifiers and never produce a character themselves.</summary>
    public static readonly IReadOnlySet<HostKey> ModifierKeys = new HashSet<HostKey>
    {
        HostKey.ShiftLeft, HostKey.ShiftRight,
        HostKey.ControlLeft, HostKey.ControlRight,
        HostKey.AltLeft, HostKey.AltRight,
        HostKey.MetaLeft, HostKey.MetaRight,
        HostKey.CapsLock,
    };

    /// <summary>
    /// Resolves a host key press to an Apple II ASCII code.
    /// Control turns a letter (and @ / [ / \ / ] / ^ / _) into its $00-$1F control code, which is
    /// how Applesoft receives CTRL-C, CTRL-G and friends.
    /// </summary>
    public static bool TryGetAscii(HostKey key, bool shift, bool control, out byte ascii)
    {
        ascii = 0;
        if (!HostKeyToAsciiMap.TryGetValue(key, out var codes))
            return false;

        ascii = shift ? codes.Shifted : codes.Plain;

        if (control && ascii >= 0x40 && ascii <= 0x5F)
            ascii = (byte)(ascii - 0x40);

        return true;
    }
}
