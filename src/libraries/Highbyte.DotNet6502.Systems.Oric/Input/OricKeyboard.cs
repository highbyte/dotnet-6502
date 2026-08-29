using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Systems.Oric.Input;

/// <summary>Eight-by-eight active-low keyboard matrix fitted to the Atmos.</summary>
public sealed class OricKeyboard
{
    private readonly byte[] _rows = new byte[8];

    private static readonly IReadOnlyDictionary<HostKey, (int Row, byte Mask)> s_keyMap =
        new Dictionary<HostKey, (int, byte)>
        {
            [HostKey.Digit3] = (0, 0x80),
            [HostKey.KeyX] = (0, 0x40),
            [HostKey.Digit1] = (0, 0x20),
            [HostKey.KeyV] = (0, 0x08),
            [HostKey.Digit5] = (0, 0x04),
            [HostKey.KeyN] = (0, 0x02),
            [HostKey.Digit7] = (0, 0x01),

            [HostKey.KeyD] = (1, 0x80),
            [HostKey.KeyQ] = (1, 0x40),
            [HostKey.Escape] = (1, 0x20),
            [HostKey.KeyF] = (1, 0x08),
            [HostKey.KeyR] = (1, 0x04),
            [HostKey.KeyT] = (1, 0x02),
            [HostKey.KeyJ] = (1, 0x01),

            [HostKey.KeyC] = (2, 0x80),
            [HostKey.Digit2] = (2, 0x40),
            [HostKey.KeyZ] = (2, 0x20),
            [HostKey.ControlLeft] = (2, 0x10),
            [HostKey.ControlRight] = (2, 0x10),
            [HostKey.Digit4] = (2, 0x08),
            [HostKey.KeyB] = (2, 0x04),
            [HostKey.Digit6] = (2, 0x02),
            [HostKey.KeyM] = (2, 0x01),

            [HostKey.Quote] = (3, 0x80),
            [HostKey.Backslash] = (3, 0x40),
            [HostKey.Minus] = (3, 0x08),
            [HostKey.Semicolon] = (3, 0x04),
            [HostKey.Digit9] = (3, 0x02),
            [HostKey.KeyK] = (3, 0x01),

            [HostKey.ArrowRight] = (4, 0x80),
            [HostKey.ArrowDown] = (4, 0x40),
            [HostKey.ArrowLeft] = (4, 0x20),
            [HostKey.ShiftLeft] = (4, 0x10),
            [HostKey.ArrowUp] = (4, 0x08),
            [HostKey.Period] = (4, 0x04),
            [HostKey.Comma] = (4, 0x02),
            [HostKey.Space] = (4, 0x01),

            [HostKey.BracketLeft] = (5, 0x80),
            [HostKey.BracketRight] = (5, 0x40),
            [HostKey.Backspace] = (5, 0x20),
            [HostKey.AltLeft] = (5, 0x10),
            [HostKey.AltRight] = (5, 0x10),
            [HostKey.KeyP] = (5, 0x08),
            [HostKey.KeyO] = (5, 0x04),
            [HostKey.KeyI] = (5, 0x02),
            [HostKey.KeyU] = (5, 0x01),

            [HostKey.KeyW] = (6, 0x80),
            [HostKey.KeyS] = (6, 0x40),
            [HostKey.KeyA] = (6, 0x20),
            [HostKey.KeyE] = (6, 0x08),
            [HostKey.KeyG] = (6, 0x04),
            [HostKey.KeyH] = (6, 0x02),
            [HostKey.KeyY] = (6, 0x01),

            [HostKey.Equal] = (7, 0x80),
            [HostKey.Enter] = (7, 0x20),
            [HostKey.ShiftRight] = (7, 0x10),
            [HostKey.Slash] = (7, 0x08),
            [HostKey.Digit0] = (7, 0x04),
            [HostKey.KeyL] = (7, 0x02),
            [HostKey.Digit8] = (7, 0x01),
        };

    public OricKeyboard() => Reset();

    public void Reset() => Array.Fill(_rows, (byte)0xff);

    public void SetKeysPressed(IReadOnlySet<HostKey> keysDown)
    {
        Reset();
        foreach (var key in keysDown)
        {
            if (s_keyMap.TryGetValue(key, out var matrixKey))
                _rows[matrixKey.Row] &= (byte)~matrixKey.Mask;
        }
    }

    public byte ReadRow(int row) => _rows[row & 0x07];

    /// <summary>
    /// PB3 is high when a pressed key in the selected row is among the columns selected by
    /// the AY port-A mask, matching the Atmos diode/transistor keyboard circuit.
    /// </summary>
    public bool IsSenseHigh(int row, byte ayPortAMask)
        => (ReadRow(row) | ayPortAMask) != 0xff;
}
