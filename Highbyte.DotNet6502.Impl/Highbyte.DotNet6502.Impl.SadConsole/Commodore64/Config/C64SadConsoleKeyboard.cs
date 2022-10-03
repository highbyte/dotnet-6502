using Highbyte.DotNet6502.Systems.Commodore64.Config;
using System.Collections.Generic;
using SadConsole.Input;

namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Config
{
    public static class C64SadConsoleKeyboard
    {
        public static List<Keys> AllModifierKeys = new()
        {
            Keys.LeftShift,
            Keys.RightShift,
            Keys.Tab,
            Keys.LeftAlt,
            Keys.RightAlt,
            Keys.LeftControl,
            Keys.RightControl,
            Keys.LeftWindows,
            Keys.RightWindows
        };

        public static Dictionary<Keys, Dictionary<Keys, byte>> SpecialKeyMaps;



        public static Dictionary<Keys, byte> SpecialKeys = new()
        {
            {Keys.Back,    0x14},
            {Keys.Enter,   0x0d},
            {Keys.Space,   0x20},
            {Keys.Down,    0x11},
            {Keys.Left,    0x9d},
            {Keys.Right,   0x1d},
            {Keys.Up,      0x91},
        };

        public static Dictionary<Keys, byte> SpecialKeysControl = new()
        {
            {Keys.D1,               (byte)Petscii.Colors.Black},
            {Keys.D2,               (byte)Petscii.Colors.White},
            {Keys.D3,               (byte)Petscii.Colors.Red},
            {Keys.D4,               (byte)Petscii.Colors.Cyan},
            {Keys.D5,               (byte)Petscii.Colors.Purple},
            {Keys.D6,               (byte)Petscii.Colors.Green},
            {Keys.D7,               (byte)Petscii.Colors.Blue},
            {Keys.D8,               (byte)Petscii.Colors.Yellow},
        };

        public static Dictionary<Keys, byte> SpecialKeysCommodore = new()
        {
            {Keys.D1,               (byte)Petscii.Colors.Orange},
            {Keys.D2,               (byte)Petscii.Colors.Brown},
            {Keys.D3,               (byte)Petscii.Colors.LightRed},
            {Keys.D4,               (byte)Petscii.Colors.DarkGray},
            {Keys.D5,               (byte)Petscii.Colors.MediumGray},
            {Keys.D6,               (byte)Petscii.Colors.LightGreen},
            {Keys.D7,               (byte)Petscii.Colors.LightBlue},
            {Keys.D8,               (byte)Petscii.Colors.LightGray},
        };

        static C64SadConsoleKeyboard()
        {
            SpecialKeyMaps = new()
            {
                {Keys.None, SpecialKeys},
                {Keys.Tab, SpecialKeysControl},
                {Keys.LeftControl, SpecialKeysCommodore},
                {Keys.RightControl, SpecialKeysCommodore},
            };
        }
    }
}
