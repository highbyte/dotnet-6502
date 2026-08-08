using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Systems.Apple2.Input;

/// <summary>
/// Maps host keys to the 7-bit ASCII codes the Apple II keyboard encoder produces.
///
/// Far simpler than a matrix machine: the encoder emits one character code per key press, so
/// there is no chord resolution to do — only the shifted/unshifted pairs plus the Control
/// modifier. The Apple II and II Plus have no lowercase, so letters always produce uppercase
/// codes regardless of Shift or Caps Lock.
///
/// Letters, digits and the control keys sit in the same place on every supported layout, so they
/// form a shared base map; punctuation and the shifted digits are layout-specific and merged in
/// by the constructor — the same split <c>C64HostKeyboard</c> uses.
/// </summary>
public class Apple2HostKeyboard
{
    /// <summary>The host keyboard layout this map was built for.</summary>
    public HostKeyboardLayout Layout { get; }

    /// <summary>
    /// The complete host key → (unshifted, shifted) ASCII map for <see cref="Layout"/>: the
    /// layout-independent base entries plus the layout-specific ones merged over them.
    /// </summary>
    public IReadOnlyDictionary<HostKey, (byte Plain, byte Shifted)> HostKeyToAsciiMap { get; }

    /// <summary>Builds the keyboard map for the given host keyboard layout.</summary>
    public Apple2HostKeyboard(HostKeyboardLayout layout)
    {
        Layout = layout;

        var map = new Dictionary<HostKey, (byte, byte)>(LayoutIndependentMap);
        var layoutSpecific = layout == HostKeyboardLayout.Swedish ? SwedishMap : USMap;
        foreach (var entry in layoutSpecific)
            map[entry.Key] = entry.Value;

        HostKeyToAsciiMap = map;
    }

    /// <summary>
    /// Keys that produce the same code on every supported layout: the letters (uppercase-only —
    /// there is no lowercase character generator), the unshifted digits, and the control keys.
    /// </summary>
    private static readonly IReadOnlyDictionary<HostKey, (byte Plain, byte Shifted)> LayoutIndependentMap =
        new Dictionary<HostKey, (byte, byte)>
        {
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

            // Unshifted digits are the same everywhere; the shifted symbols are not, so every
            // digit is re-stated in each layout map below.
            { HostKey.Digit1, ((byte)'1', (byte)'1') },
            { HostKey.Digit2, ((byte)'2', (byte)'2') },
            { HostKey.Digit3, ((byte)'3', (byte)'3') },
            { HostKey.Digit4, ((byte)'4', (byte)'4') },
            { HostKey.Digit5, ((byte)'5', (byte)'5') },
            { HostKey.Digit6, ((byte)'6', (byte)'6') },
            { HostKey.Digit7, ((byte)'7', (byte)'7') },
            { HostKey.Digit8, ((byte)'8', (byte)'8') },
            { HostKey.Digit9, ((byte)'9', (byte)'9') },
            { HostKey.Digit0, ((byte)'0', (byte)'0') },

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

    /// <summary>US (ANSI) punctuation and shifted digits — what the map used to hard-code.</summary>
    private static readonly IReadOnlyDictionary<HostKey, (byte Plain, byte Shifted)> USMap =
        new Dictionary<HostKey, (byte, byte)>
        {
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
        };

    /// <summary>
    /// Swedish (ISO) punctuation and shifted digits.
    ///
    /// Two kinds of entry, and the distinction matters when changing this map:
    ///
    /// <para><b>1. What the Swedish layout genuinely produces</b> — the shifted digits, and the
    /// keys right of L and right of M. A user gets the character printed on the keycap.</para>
    ///
    /// <para><b>2. Convenience bindings for the Å/Ä/Ö keys.</b> Those three letters have no 7-bit
    /// ASCII form, so the Apple II cannot receive them and the keys would otherwise be dead. They
    /// are bound to <c>[ ] \</c> (and shifted, <c>{ } |</c>) — characters a Swedish keyboard can
    /// otherwise only reach through Alt/Option chords that differ between macOS and Windows.
    /// <c>C64HostKeyboard</c> takes the same approach with its Ä/Å bindings.</para>
    ///
    /// <para>Alt chords are bound only where macOS and Windows agree (<c>@ $ [ ]</c>). Others
    /// diverge — Alt+7 is <c>{</c> on Windows but <c>|</c> on macOS — so binding them would be
    /// wrong on one platform or the other, and the Å/Ä/Ö bindings above already cover the gap.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyDictionary<HostKey, (byte Plain, byte Shifted)> SwedishMap =
        new Dictionary<HostKey, (byte, byte)>
        {
            // Digit row. Shift+4 is ¤ on Windows and $ on macOS; ¤ has no ASCII form, so $ is
            // used for both — correct on macOS, and a usable $ rather than nothing on Windows.
            { HostKey.Digit1, ((byte)'1', (byte)'!') },
            { HostKey.Digit2, ((byte)'2', (byte)'"') },
            { HostKey.Digit3, ((byte)'3', (byte)'#') },
            { HostKey.Digit4, ((byte)'4', (byte)'$') },
            { HostKey.Digit5, ((byte)'5', (byte)'%') },
            { HostKey.Digit6, ((byte)'6', (byte)'&') },
            { HostKey.Digit7, ((byte)'7', (byte)'/') },
            { HostKey.Digit8, ((byte)'8', (byte)'(') },
            { HostKey.Digit9, ((byte)'9', (byte)')') },
            { HostKey.Digit0, ((byte)'0', (byte)'=') },

            // Right of 0: the +/? key, then the ´/` dead key. A dead key produces no character
            // on its own, so only the ^ on the ¨ key (below) is worth binding.
            { HostKey.Minus, ((byte)'+', (byte)'?') },

            // Right of P: Å, then the ¨/^ dead key. ^ is Applesoft's exponent operator, so the
            // shifted position is bound even though the unshifted ¨ is not.
            { HostKey.BracketLeft, ((byte)'[', (byte)'{') },   // Å — convenience, see remarks
            { HostKey.BracketRight, (0x00, (byte)'^') },

            // Right of L: Ö, Ä, then the '/* key.
            { HostKey.Semicolon, ((byte)'\\', (byte)'|') },    // Ö — convenience, see remarks
            { HostKey.Quote, ((byte)']', (byte)'}') },         // Ä — convenience, see remarks
            { HostKey.Backslash, ((byte)'\'', (byte)'*') },

            // Left of Z: the <> key.
            { HostKey.IntlBackslash, ((byte)'<', (byte)'>') },

            // Right of M: comma, period, then the -/_ key.
            { HostKey.Comma, ((byte)',', (byte)';') },
            { HostKey.Period, ((byte)'.', (byte)':') },
            { HostKey.Slash, ((byte)'-', (byte)'_') },

            // Left of 1 is §/½ on a Swedish keyboard — no ASCII form, so unbound. (On macOS it
            // arrives as IntlBackslash; Apple2InputHandler corrects that before lookup.)
        };

    /// <summary>
    /// Alt/Option + digit chords that produce a character on a Swedish keyboard, limited to the
    /// ones macOS and Windows agree on. Checked before the plain map so the chord wins over the
    /// digit it is built from.
    /// </summary>
    private static readonly IReadOnlyDictionary<HostKey, byte> SwedishAltChords =
        new Dictionary<HostKey, byte>
        {
            { HostKey.Digit2, (byte)'@' },
            { HostKey.Digit4, (byte)'$' },
            { HostKey.Digit8, (byte)'[' },
            { HostKey.Digit9, (byte)']' },
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

    /// <summary>Whether the key produces a character on this layout (so is worth latching).</summary>
    public bool ProducesCharacter(HostKey key) =>
        HostKeyToAsciiMap.ContainsKey(key)
        || (Layout == HostKeyboardLayout.Swedish && SwedishAltChords.ContainsKey(key));

    /// <summary>
    /// Resolves a host key press to an Apple II ASCII code.
    /// Control turns a letter (and @ / [ / \ / ] / ^ / _) into its $00-$1F control code, which is
    /// how Applesoft receives CTRL-C, CTRL-G and friends.
    /// </summary>
    public bool TryGetAscii(HostKey key, bool shift, bool control, out byte ascii, bool alt = false)
    {
        ascii = 0;

        if (alt && Layout == HostKeyboardLayout.Swedish && SwedishAltChords.TryGetValue(key, out var altAscii))
        {
            ascii = altAscii;
        }
        else
        {
            if (!HostKeyToAsciiMap.TryGetValue(key, out var codes))
                return false;

            ascii = shift ? codes.Shifted : codes.Plain;

            // A dead key's unshifted half has no character (e.g. Swedish ¨), marked as 0x00.
            if (ascii == 0x00)
                return false;
        }

        if (control && ascii >= 0x40 && ascii <= 0x5F)
            ascii = (byte)(ascii - 0x40);

        return true;
    }
}
