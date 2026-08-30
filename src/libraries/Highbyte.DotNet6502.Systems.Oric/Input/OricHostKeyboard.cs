using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Systems.Oric.Input;

/// <summary>
/// Translates a host keyboard layout into the US-style physical key positions in the Atmos
/// keyboard matrix. Letters and control keys are already in matching positions; Swedish
/// punctuation sometimes needs a different Oric key and/or a synthetic Oric Shift press.
/// </summary>
public sealed class OricHostKeyboard
{
    private readonly record struct MappedKey(HostKey Key, bool Shift, bool ConsumesAlt = false);

    private static readonly IReadOnlyDictionary<(HostKey Key, bool Shift, bool Alt), MappedKey>
        s_swedishMap = new Dictionary<(HostKey, bool, bool), MappedKey>
        {
            // Swedish shifted digit row: ! " # $ % & / ( ) =.
            [(HostKey.Digit2, true, false)] = new(HostKey.Quote, true),
            [(HostKey.Digit6, true, false)] = new(HostKey.Digit7, true),
            [(HostKey.Digit7, true, false)] = new(HostKey.Slash, false),
            [(HostKey.Digit8, true, false)] = new(HostKey.Digit9, true),
            [(HostKey.Digit9, true, false)] = new(HostKey.Digit0, true),
            [(HostKey.Digit0, true, false)] = new(HostKey.Equal, false),

            // Right of 0 is +/?; -/_ is on the key right of period.
            [(HostKey.Minus, false, false)] = new(HostKey.Equal, true),
            [(HostKey.Minus, true, false)] = new(HostKey.Slash, true),
            [(HostKey.Slash, false, false)] = new(HostKey.Minus, false),
            [(HostKey.Slash, true, false)] = new(HostKey.Minus, true),

            // ;/: live on shifted comma/period. The '/* key is right of Ä.
            [(HostKey.Comma, true, false)] = new(HostKey.Semicolon, false),
            [(HostKey.Period, true, false)] = new(HostKey.Semicolon, true),
            [(HostKey.Backslash, false, false)] = new(HostKey.Quote, false),
            [(HostKey.Backslash, true, false)] = new(HostKey.Digit8, true),

            // The ISO key left of Z is </>.
            [(HostKey.IntlBackslash, false, false)] = new(HostKey.Comma, true),
            [(HostKey.IntlBackslash, true, false)] = new(HostKey.Period, true),

            // Å/Ä/Ö have no Atmos glyphs, so bind them to otherwise awkward [ ] \ keys.
            [(HostKey.BracketLeft, false, false)] = new(HostKey.BracketLeft, false),
            [(HostKey.BracketLeft, true, false)] = new(HostKey.BracketLeft, true),
            [(HostKey.Quote, false, false)] = new(HostKey.BracketRight, false),
            [(HostKey.Quote, true, false)] = new(HostKey.BracketRight, true),
            [(HostKey.Semicolon, false, false)] = new(HostKey.Backslash, false),
            [(HostKey.Semicolon, true, false)] = new(HostKey.Backslash, true),

            // The unshifted ¨ dead key produces nothing; shifted it produces ^.
            [(HostKey.BracketRight, false, false)] = new(HostKey.None, false),
            [(HostKey.BracketRight, true, false)] = new(HostKey.Digit6, true),

            // The Swedish ´/` dead key and §/½ key do not have useful standalone Atmos forms.
            [(HostKey.Equal, false, false)] = new(HostKey.None, false),
            [(HostKey.Equal, true, false)] = new(HostKey.None, false),
            [(HostKey.Backquote, false, false)] = new(HostKey.None, false),
            [(HostKey.Backquote, true, false)] = new(HostKey.None, false),

            // Alt/Option/AltGr chords common to both macOS and Windows Swedish layouts.
            [(HostKey.Digit2, false, true)] = new(HostKey.Digit2, true, ConsumesAlt: true),
            [(HostKey.Digit4, false, true)] = new(HostKey.Digit4, true, ConsumesAlt: true),
            [(HostKey.Digit8, false, true)] = new(HostKey.BracketLeft, false, ConsumesAlt: true),
            [(HostKey.Digit9, false, true)] = new(HostKey.BracketRight, false, ConsumesAlt: true),
        };

    public OricHostKeyboard(HostKeyboardLayout layout) => Layout = layout;

    /// <summary>The host keyboard layout this translator was built for.</summary>
    public HostKeyboardLayout Layout { get; }

    /// <summary>
    /// Replaces <paramref name="destination"/> with Oric matrix keys for the held host keys.
    /// The caller owns and reuses the destination set so held input does not allocate per frame.
    /// </summary>
    public void Translate(IReadOnlySet<HostKey> source, HashSet<HostKey> destination)
    {
        destination.Clear();
        if (Layout == HostKeyboardLayout.US)
        {
            destination.UnionWith(source);
            return;
        }

        var sourceShift = source.Contains(HostKey.ShiftLeft) || source.Contains(HostKey.ShiftRight);
        var sourceAlt = source.Contains(HostKey.AltLeft) || source.Contains(HostKey.AltRight);
        var targetShift = false;
        var altConsumed = false;
        var hasCharacterKey = false;

        foreach (var key in source)
        {
            if (IsShift(key) || IsAlt(key))
                continue;

            if (IsOtherModifier(key))
            {
                destination.Add(key);
                continue;
            }

            hasCharacterKey = true;
            // A recognised AltGr/Option chord wins. All other Alt combinations still use the
            // Swedish base key mapping while preserving Alt as the Atmos FUNCT modifier.
            var mapped = default(MappedKey);
            var hasMapping = sourceAlt &&
                s_swedishMap.TryGetValue((key, sourceShift, true), out mapped);
            if (!hasMapping)
                hasMapping = s_swedishMap.TryGetValue((key, sourceShift, false), out mapped);
            if (hasMapping)
            {
                if (mapped.Key != HostKey.None)
                    destination.Add(mapped.Key);
                targetShift |= mapped.Shift;
                altConsumed |= mapped.ConsumesAlt;
                continue;
            }

            destination.Add(key);
            targetShift |= sourceShift;
        }

        if (targetShift)
        {
            destination.Add(HostKey.ShiftRight);
        }
        else if (!hasCharacterKey)
        {
            if (source.Contains(HostKey.ShiftLeft))
                destination.Add(HostKey.ShiftLeft);
            if (source.Contains(HostKey.ShiftRight))
                destination.Add(HostKey.ShiftRight);
        }

        if (!altConsumed)
        {
            if (source.Contains(HostKey.AltLeft))
                destination.Add(HostKey.AltLeft);
            if (source.Contains(HostKey.AltRight))
                destination.Add(HostKey.AltRight);
        }
    }

    private static bool IsShift(HostKey key) => key is HostKey.ShiftLeft or HostKey.ShiftRight;

    private static bool IsAlt(HostKey key) => key is HostKey.AltLeft or HostKey.AltRight;

    private static bool IsOtherModifier(HostKey key) => key is
        HostKey.ControlLeft or HostKey.ControlRight or
        HostKey.MetaLeft or HostKey.MetaRight or
        HostKey.CapsLock;
}
