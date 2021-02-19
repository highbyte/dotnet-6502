using System;

namespace SadConsoleTest
{
    public class EmulatorScreenConfig
    {
        public int Cols { get; set; }
        public int Rows { get; set; }

        public ushort ScreenStartAddress { get; set; }
        public ushort ScreenColorStartAddress { get; set; }

        public ushort ScreenRefreshStatusAddress { get; set; }
        public ushort ScreenBackgroundColorAddress { get; set; }

        public byte DefaultFgColor  { get; set; }
        public byte DefaultBgColor  { get; set; }

        public EmulatorScreenConfig()
        {
            Cols = 80;
            Rows = 25;

            // Mimic C64 for some memory addresses (though we have 80 cols here instead of 40)
            ScreenStartAddress = 0x0400;
            ScreenColorStartAddress = 0xd800;

            ScreenRefreshStatusAddress = 0xd000;
            //ScreenBorderColorAddress = 0xd020;
            ScreenBackgroundColorAddress = 0xd021;

            DefaultFgColor = 0x0e;  // 0x0e = Light blue
            DefaultBgColor = 0x06;  // 0x06 = Blue
        }
    }

    public enum ScreenStatusBitFlags: int
    {
        HostNewFrame = 0,
        EmulatorDoneForFrame = 1,
    }
}
