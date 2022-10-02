using System;

namespace Highbyte.DotNet6502.Systems.Commodore64
{
    public class Vic2
    {
        public const ushort COLS = 40;
        public const ushort ROWS = 25;

        public const int PIXELS_PER_LINE = 504;    // including border
        // PAL
        public const int PAL_CYCLES_PER_LINE = 63;
        public const int PAL_LINES = 312;
        public const int PAL_CYCLES_PER_FRAME = PAL_CYCLES_PER_LINE * PAL_LINES;
        // NTSC new machines
        public const int NTSC_NEW_CYCLES_PER_LINE = 64;
        public const int NTSC_NEW_LINES = 262;
        public const int NTSC_NEW_CYCLES_PER_FRAME = NTSC_NEW_CYCLES_PER_LINE * NTSC_NEW_LINES;
        // NTSC old machines
        public const int NTSC_OLD_CYCLES_PER_LINE = 65;
        public const int NTSC_OLD_LINES = 263;
        public const int NTSC_OLD_CYCLES_PER_FRAME = NTSC_OLD_CYCLES_PER_LINE * NTSC_OLD_LINES;

        private ulong _cyclesConsumedCurrentVblank = 0;

        public byte BorderColor { get; private set; }
        public byte BackgroundColor { get; private set; }

        public void BorderColorStore(ushort _, byte value)
        {
            BorderColor = value;
        }
        public byte BorderColorLoad(ushort _)
        {
            return BorderColor;
        }
        public void BackgroundColorStore(ushort _, byte value)
        {
            BackgroundColor = value;
        }
        public byte BackgroundColorLoad(ushort _)
        {
            return BackgroundColor;
        }

        public void VerticalBlank(CPU cpu)
        {
            _cyclesConsumedCurrentVblank = 0;

            // Issue vertical blank signal to CPU (typically issue a IRQ, will take effect next frame)
            // TODO: This assumes the IRQ always occurs for raster line 0. In a real Vic2 (C64) the line to generate the IRQ is configurable in 0xd012
            cpu.IRQ = true;
        }

        public void CPUCyclesConsumed(CPU cpu, Memory mem, ulong cyclesConsumed)
        {
            _cyclesConsumedCurrentVblank += cyclesConsumed;
            UpdateCurrentRasterLine(mem);
        }

        private void UpdateCurrentRasterLine(Memory mem)
        {
            // Calculate the current raster line based on how man CPU cycles has been executed this frame
            var line = (ushort)(_cyclesConsumedCurrentVblank / NTSC_NEW_CYCLES_PER_LINE);
            // Bits 0-7 of current line stored in 0xd012
            mem[Vic2Addr.CURRENT_RASTER_LINE] = (byte)(line & 0xff);
            // Bit 8 of current line stored in 0xd011 bit #7
            var screenControlReg1Value = mem[Vic2Addr.SCREEN_CONTROL_REGISTER_1];
            if (line > NTSC_NEW_LINES)
                throw new Exception("Internal error. Unreachable scan line. The CPU probably executed more cycles current frame than allowed.");
            if (line <= 255)
                screenControlReg1Value.ClearBit(7);
            else
                screenControlReg1Value.SetBit(7);
            mem[Vic2Addr.SCREEN_CONTROL_REGISTER_1] = screenControlReg1Value;
        }
    }
}
