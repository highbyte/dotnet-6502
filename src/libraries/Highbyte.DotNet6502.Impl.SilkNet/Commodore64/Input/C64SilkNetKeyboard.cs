using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Input;

public static class C64SilkNetKeyboard
{
    public static List<Key> AllModifierKeys = new()
    {
        Key.ShiftLeft,
        Key.ShiftRight,
        Key.Tab,
        Key.AltLeft,
        Key.AltRight,
        Key.ControlLeft,
        Key.ControlRight,
        // Key.WindowsLeft,
        // Key.WindowsRight
    };

    public static Dictionary<Key, Dictionary<Key, byte>> SpecialKeyMaps;

    // Map of special SilkNet keys to their PETSCII codes.
    public static Dictionary<Key, byte> SpecialKeys = new()
    {
        {Key.Backspace,     0x14},
        {Key.Enter,         0x0d},
        {Key.Space,         0x20},
        {Key.Down,          0x11},
        {Key.Left,          0x9d},
        {Key.Right,         0x1d},
        {Key.Up,            0x91},
    };

    // Map of special SilkNet keys to their PETSCII codes.
    public static Dictionary<Key, byte> SpecialKeysControl = new()
    {
        {Key.Number1,               (byte)Petscii.Colors.Black},
        {Key.Number2,               (byte)Petscii.Colors.White},
        {Key.Number3,               (byte)Petscii.Colors.Red},
        {Key.Number4,               (byte)Petscii.Colors.Cyan},
        {Key.Number5,               (byte)Petscii.Colors.Purple},
        {Key.Number6,               (byte)Petscii.Colors.Green},
        {Key.Number7,               (byte)Petscii.Colors.Blue},
        {Key.Number8,               (byte)Petscii.Colors.Yellow},
    };

    // Map of special SilkNet keys to their PETSCII codes.
    public static Dictionary<Key, byte> SpecialKeysCommodore = new()
    {
        {Key.Number1,               (byte)Petscii.Colors.Orange},
        {Key.Number2,               (byte)Petscii.Colors.Brown},
        {Key.Number3,               (byte)Petscii.Colors.LightRed},
        {Key.Number4,               (byte)Petscii.Colors.DarkGray},
        {Key.Number5,               (byte)Petscii.Colors.MediumGray},
        {Key.Number6,               (byte)Petscii.Colors.LightGreen},
        {Key.Number7,               (byte)Petscii.Colors.LightBlue},
        {Key.Number8,               (byte)Petscii.Colors.LightGray},
    };

    static C64SilkNetKeyboard()
    {
        SpecialKeyMaps = new()
        {
            {Key.Unknown, SpecialKeys},
            {Key.Tab, SpecialKeysControl},
            {Key.ControlLeft, SpecialKeysCommodore},
            {Key.ControlRight, SpecialKeysCommodore},
        };
    }
}
