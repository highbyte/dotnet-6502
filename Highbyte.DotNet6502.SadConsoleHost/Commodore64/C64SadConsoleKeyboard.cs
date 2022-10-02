using Highbyte.DotNet6502.Systems.Commodore64.Config;
using System.Collections.Generic;
using SadConsole.Input;

namespace Highbyte.DotNet6502.SadConsoleHost.Commodore64
{
    public static class C64SadConsoleKeyboard
    {
        public static List<Keys> AllModifierKeys = new()
        {
            Keys.LeftShift,
            Keys.RightShift,
            Keys.Tab,
            Keys.LeftControl,
            Keys.RightControl,
            // Keys.LeftAlt,
            // Keys.RightAlt,
            // Keys.LeftWindows,
            // Keys.RightWindows
        };

        public static Dictionary<Keys, Dictionary<SadConsole.Input.Keys, byte>> KeyMaps;

        public static Dictionary<SadConsole.Input.Keys, byte> KeysNormal = new()
        {
            {Keys.Back,             0x14},
            {Keys.Enter,            0x0d},
            {Keys.Space,            0x20},
            {Keys.OemQuotes,        0x27},
            {Keys.OemComma,         0x2c},
            {Keys.OemPeriod,        0x2e},
            {Keys.Divide,           0x2f},
            {Keys.D0,               0x30},
            {Keys.D1,               0x31},
            {Keys.D2,               0x32},
            {Keys.D3,               0x33},
            {Keys.D4,               0x34},
            {Keys.D5,               0x35},
            {Keys.D6,               0x36},
            {Keys.D7,               0x37},
            {Keys.D8,               0x38},
            {Keys.D9,               0x39},
            {Keys.OemSemicolon,     0x3b},
            //{Keys.Equals,           0x3d}, // TODO: Why no equals sign enum value in SadConsole?
            {Keys.OemOpenBrackets,  0x5b},
            {Keys.OemBackslash,     0x5c},
            {Keys.OemCloseBrackets, 0x5d},
            {Keys.A,                0x41},
            {Keys.B,                0x42},
            {Keys.C,                0x43},
            {Keys.D,                0x44},
            {Keys.E,                0x45},
            {Keys.F,                0x46},
            {Keys.G,                0x47},
            {Keys.H,                0x48},
            {Keys.I,                0x49},
            {Keys.J,                0x4a},
            {Keys.K,                0x4b},
            {Keys.L,                0x4c},
            {Keys.M,                0x4d},
            {Keys.N,                0x4e},
            {Keys.O,                0x4f},
            {Keys.P,                0x50},
            {Keys.Q,                0x51},
            {Keys.R,                0x52},
            {Keys.S,                0x53},
            {Keys.T,                0x54},
            {Keys.U,                0x55},
            {Keys.V,                0x56},
            {Keys.W,                0x57},
            {Keys.X,                0x58},
            {Keys.Y,                0x59},
            {Keys.Z,                0x5a},

            {Keys.Down,             0x11},
            {Keys.Left,             0x9d},
            {Keys.Right,            0x1d},
            {Keys.Up,               0x91},
        };

        public static Dictionary<SadConsole.Input.Keys, byte> KeysShift = new()
        {
            {Keys.OemQuotes,        0x22},
            {Keys.OemPeriod,        0x3e},
            {Keys.OemComma,         0x3c},
            {Keys.Divide,           0x3f},
            {Keys.D0,               0x29},
            {Keys.D1,               0x21},
            {Keys.D2,               0x40},
            {Keys.D3,               0x23},
            {Keys.D4,               0x24},
            {Keys.D5,               0x25},
            {Keys.D6,               0x5e},
            {Keys.D7,               0x26},
            {Keys.D8,               0x2a},
            {Keys.D9,               0x28},

            {Keys.OemSemicolon,     0x3a},
            //{Keys.Equals,           0x2b},  // TODO: Why no equals sign enum value in SadConsole?

            {Keys.A,                0xc1},
            {Keys.B,                0xc2},
            {Keys.C,                0xc3},
            {Keys.D,                0xc4},
            {Keys.E,                0xc5},
            {Keys.F,                0xc6},
            {Keys.G,                0xc7},
            {Keys.H,                0xc8},
            {Keys.I,                0xc9},
            {Keys.J,                0xca},
            {Keys.K,                0xcb},
            {Keys.L,                0xcc},
            {Keys.M,                0xcd},
            {Keys.N,                0xce},
            {Keys.O,                0xcf},
            {Keys.P,                0xd0},
            {Keys.Q,                0xd1},
            {Keys.R,                0xd2},
            {Keys.S,                0xd3},
            {Keys.T,                0xd4},
            {Keys.U,                0xd5},
            {Keys.V,                0xd6},
            {Keys.W,                0xd7},
            {Keys.X,                0xd8},
            {Keys.Y,                0xd9},
            {Keys.Z,                0xda},
        };

        public static Dictionary<SadConsole.Input.Keys, byte> KeysControl = new()
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

        public static Dictionary<SadConsole.Input.Keys, byte> KeysCommodore = new()
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
            KeyMaps = new()
            {
                {Keys.None, KeysNormal},
                {Keys.LeftShift, KeysShift},
                {Keys.RightShift, KeysShift},
                {Keys.Tab, KeysControl},
                {Keys.LeftControl, KeysCommodore},
                {Keys.RightControl, KeysCommodore},
            };
        }
    }
}
