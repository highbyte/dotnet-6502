using System;
using System.Collections.Generic;

namespace SadConsoleTest
{
    public class EmulatorMemoryConfig
    {
        public EmulatorScreenConfig EmulatorScreenConfig { get; set; }
        public EmulatorInputConfig EmulatorInputConfig { get; set; }
       
        public EmulatorMemoryConfig()
        {
            EmulatorScreenConfig = new();
            EmulatorInputConfig = new();
        }

        public void Validate()
        {
            ushort screenMemoryStart = EmulatorScreenConfig.ScreenStartAddress;
            ushort screenMemoryEnd = (ushort)(screenMemoryStart + (ushort)(EmulatorScreenConfig.Cols * EmulatorScreenConfig.Rows));

            ushort colorMemoryStart = EmulatorScreenConfig.ScreenColorStartAddress;
            ushort colorMemoryEnd = (ushort)(colorMemoryStart + (ushort)(EmulatorScreenConfig.Cols * EmulatorScreenConfig.Rows));

            // --------------------------------
            // Screen & color addresses
            // --------------------------------
            // Validate so screen memory and color memory don't overlap
            if(Overlap(screenMemoryStart, screenMemoryEnd, colorMemoryStart, colorMemoryEnd))
                throw new Exception("Screen and Color memory address space overlaps");

            // Validate so Background color address falls within screen or color addresses
            if(Within(EmulatorScreenConfig.ScreenBackgroundColorAddress, screenMemoryStart, screenMemoryEnd))
                throw new Exception("ScreenBackgroundColorAddress cannot be in screen memory.");
            if(Within(EmulatorScreenConfig.ScreenBackgroundColorAddress, colorMemoryStart, colorMemoryEnd))
                throw new Exception("ScreenBackgroundColorAddress cannot be in screen memory.");

            // Validate so Border color address falls within screen or color addresses
            if(Within(EmulatorScreenConfig.ScreenBorderColorAddress, screenMemoryStart, screenMemoryEnd))
                throw new Exception("ScreenBorderColorAddress cannot be in screen memory.");
            if(Within(EmulatorScreenConfig.ScreenBorderColorAddress, colorMemoryStart, colorMemoryEnd))
                throw new Exception("ScreenBorderColorAddress cannot be in screen memory.");

            // --------------------------------
            // Input addresses
            // --------------------------------
            // Validate so Keyboard input address falls within screen or color addresses
            if(Within(EmulatorInputConfig.KeyPressedAddress, screenMemoryStart, screenMemoryEnd))
                throw new Exception("KeyPressedAddress cannot be in screen memory.");
            if(Within(EmulatorInputConfig.KeyPressedAddress, colorMemoryStart, colorMemoryEnd))
                throw new Exception("KeyPressedAddress cannot be in color memory.");
            if(Within(EmulatorInputConfig.KeyDownAddress, screenMemoryStart, screenMemoryEnd))
                throw new Exception("KeyDownAddress cannot be in screen memory.");
            if(Within(EmulatorInputConfig.KeyDownAddress, colorMemoryStart, colorMemoryEnd))
                throw new Exception("KeyDownAddress cannot be in color memory.");
            if(Within(EmulatorInputConfig.KeyReleasedAddress, screenMemoryStart, screenMemoryEnd))
                throw new Exception("KeyReleasedAddress cannot be in color memory.");
            if(Within(EmulatorInputConfig.KeyReleasedAddress, colorMemoryStart, colorMemoryEnd))
                throw new Exception("KeyReleasedAddress cannot be in color memory.");


            // --------------------------------
            // Character and color maps
            // --------------------------------
            if(!EmulatorScreenConfig.UseAscIICharacters && EmulatorScreenConfig.CharacterMap==null)
                throw new Exception($"If {nameof(EmulatorScreenConfig.UseAscIICharacters)} is false, {nameof(EmulatorScreenConfig.CharacterMap)} must be set to a character map.");

        }

        private bool Within(ushort address, ushort startAddress, ushort endAddress)
        {
            return address >= startAddress && address <= endAddress;
        }

        private bool Overlap(ushort address1Start, ushort address1End, ushort address2Start, ushort address2End)
        {
            return !(address1End < address2Start || address1Start > address2End);
        }
    }

    public class EmulatorScreenConfig
    {
        public int Cols { get; set; }
        public int Rows { get; set; }
        public int BorderCols { get; set; }
        public int BorderRows { get; set; }

        public ushort ScreenStartAddress { get; set; }
        public ushort ScreenColorStartAddress { get; set; }

        public ushort ScreenRefreshStatusAddress { get; set; }

        public ushort ScreenBorderColorAddress { get; set; }
        public ushort ScreenBackgroundColorAddress { get; set; }

        public byte DefaultFgColor  { get; set; }
        public byte DefaultBgColor  { get; set; }
        public byte DefaultBorderColor  { get; set; }

        public Dictionary<byte,Microsoft.Xna.Framework.Color> ColorMap { get; set; }
        public bool UseAscIICharacters { get; internal set; }

        /// <summary>
        /// If UseAscIICharacters is false, set a custom character map in CharacterMap
        /// </summary>
        /// <value></value>
        public Dictionary<byte,byte> CharacterMap { get; set; }

        public EmulatorScreenConfig()
        {
            Cols = 80;
            Rows = 25;
            BorderCols = 0;
            BorderRows = 0;

            // Mimic C64 for some memory addresses (though we have 80 cols here instead of 40)
            ScreenStartAddress              = 0x0400;   //80*25 = 2000(0x07d0) -> range 0x0400 - 0x0bcf
            ScreenColorStartAddress         = 0xd800;   //80*25 = 2000(0x07d0) -> range 0xd800 - 0xdfcf
            ScreenRefreshStatusAddress      = 0xd000;

            ScreenBorderColorAddress        = 0xd020;
            ScreenBackgroundColorAddress    = 0xd021;

            DefaultFgColor                  = 0x0e;  // 0x0e = Light blue
            DefaultBgColor                  = 0x06;  // 0x06 = Blue
            DefaultBorderColor              = 0x0e;  // 0x0e = Blue

            ColorMap = SadConsoleEmulatorColorMaps.C64ColorMap; // Default to C64 color map

            UseAscIICharacters = true;

            //CharacterMap = SadConsoleEmulatorCharacterMaps.PETSCIIMap; // TODO
        }
    }

    public class EmulatorInputConfig
    {
        // Keyboard
        public ushort KeyPressedAddress { get; set; }
        public ushort KeyDownAddress { get; set; }
        public ushort KeyReleasedAddress { get; set; }

        public EmulatorInputConfig()
        {
            KeyPressedAddress       = 0xe000;
            KeyDownAddress          = 0xe001;
            KeyReleasedAddress      = 0xe002;
        }
    }

    public enum ScreenStatusBitFlags: int
    {
        HostNewFrame = 0,
        EmulatorDoneForFrame = 1,
    }
}
