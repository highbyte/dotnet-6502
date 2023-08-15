using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64.Input;

public static class C64AspNetKeyboard
{
    public static List<string> AllModifierKeys = new()
    {
        "ShiftLeft",
        "ShiftRight",
        "Tab",
        "AltLeft",
        "AltRight",
        "ControlLeft",
        "ControlRight",
        // "Windows",
    };

    public static Dictionary<string, Dictionary<string, byte>> SpecialKeyMaps;

    public static Dictionary<string, byte> SpecialKeys = new()
    {
        {"Backspace",     0x14},
        //{"Enter",         0x0d},      // Enter received in KeyPressed as normal characters
        //{"Space",         0x20},      // Space received in KeyPressed as normal characters
        {"ArrowDown",     0x11},
        {"ArrowLeft",     0x9d},
        {"ArrowRight",    0x1d},
        {"ArrowUp",       0x91},
    };

    public static Dictionary<string, byte> SpecialKeysControl = new()
    {
        {"1",               (byte)Petscii.Colors.Black},
        {"2",               (byte)Petscii.Colors.White},
        {"3",               (byte)Petscii.Colors.Red},
        {"4",               (byte)Petscii.Colors.Cyan},
        {"5",               (byte)Petscii.Colors.Purple},
        {"6",               (byte)Petscii.Colors.Green},
        {"7",               (byte)Petscii.Colors.Blue},
        {"8",               (byte)Petscii.Colors.Yellow},
    };

    public static Dictionary<string, byte> SpecialKeysCommodore = new()
    {
        {"1",               (byte)Petscii.Colors.Orange},
        {"2",               (byte)Petscii.Colors.Brown},
        {"3",               (byte)Petscii.Colors.LightRed},
        {"4",               (byte)Petscii.Colors.DarkGray},
        {"5",               (byte)Petscii.Colors.MediumGray},
        {"6",               (byte)Petscii.Colors.LightGreen},
        {"7",               (byte)Petscii.Colors.LightBlue},
        {"8",               (byte)Petscii.Colors.LightGray},
    };

    static C64AspNetKeyboard()
    {
        SpecialKeyMaps = new()
        {
            {"", SpecialKeys},
            {"Tab", SpecialKeysControl},
            {"ControlLeft", SpecialKeysCommodore},
        };
    }

    public static Dictionary<string, char> AspNetKeyStringToCharacter = new()
    {
        { "Enter", (char)13 },
        { "Space", ' ' },
    };
}
