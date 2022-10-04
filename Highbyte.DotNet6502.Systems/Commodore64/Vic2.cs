using System;

namespace Highbyte.DotNet6502.Systems.Commodore64
{
    public class Vic2
    {
        public const ushort COLS = 40;
        public const ushort ROWS = 25;
        public const ushort WIDTH = 320;    // Pixels
        public const ushort HEIGHT = 200;   // Pixels


        public const int PIXELS_PER_CPU_CYCLE = 8;
        public const int HBLANK_PIXELS = 101;
        public const int VBLANK_LINES = 30;

        // PAL
        public const int PAL_PIXELS_PER_LINE_VISIBLE = PAL_PIXELS_PER_LINE - HBLANK_PIXELS; // 504 - 101 = 403 pixels. Including visible border (excluding time to reach next line, HBLANK)
        public const int PAL_PIXELS_PER_LINE = 416;   // TODO
        //public const int PAL_PIXELS_PER_LINE = PAL_CYCLES_PER_LINE * PIXELS_PER_CPU_CYCLE;  // 63 * 8 = 504 pixels. Including border and time to reach next line (HBLANK)
        public const int PAL_CYCLES_PER_LINE = 63;
        public const int PAL_LINES = 312;
        public const int PAL_LINES_VISIBLE = 284;                                             // TODO: Why is it 284? Shouldn't it be 312 - 30 = 282?
        //public const int PAL_LINES_VISIBLE = PAL_LINES - VBLANK_LINES;                      // 312 - 30 = 282. Total vertical lines excluding lines spent in VBLANK.
        public const int PAL_CYCLES_PER_FRAME = PAL_CYCLES_PER_LINE * PAL_LINES;

        // NTSC new machines
        public const int NTSC_NEW_PIXELS_PER_LINE_VISIBLE = NTSC_NEW_PIXELS_PER_LINE - HBLANK_PIXELS; // 411. Including visible border (excluding time to reach next line, HBLANK)
        
        public const int NTSC_NEW_PIXELS_PER_LINE = 431; // TODO
        //public const int NTSC_NEW_PIXELS_PER_LINE = NTSC_NEW_CYCLES_PER_LINE * PIXELS_PER_CPU_CYCLE;  // 512. Including border and time to reach next line (HBLANK)
        public const int NTSC_NEW_CYCLES_PER_LINE = 64;
        public const int NTSC_NEW_LINES = 262;
        public const int NTSC_NEW_LINES_VISIBLE = 234;                                             // TODO: Why is it 234? Shouldn't it be 262 - 30 = 232?
        //public const int NTSC_NEW_LINES_VISIBLE = NTSC_NEW_LINES - VBLANK_LINES;                 // 262 - 30 = 232. Total vertical lines excluding lines spent in VBLANK.

        public const int NTSC_NEW_CYCLES_PER_FRAME = NTSC_NEW_CYCLES_PER_LINE * NTSC_NEW_LINES;
        // NTSC old machines
        public const int NTSC_OLD_PIXELS_PER_LINE_VISIBLE = NTSC_OLD_PIXELS_PER_LINE - HBLANK_PIXELS; // 419. Including visible border (excluding time to reach next line, HBLANK)
        public const int NTSC_OLD_PIXELS_PER_LINE = 439; // TODO
        //public const int NTSC_OLD_PIXELS_PER_LINE = NTSC_OLD_CYCLES_PER_LINE * PIXELS_PER_CPU_CYCLE;  // 520. Including border and time to reach next line (HBLANK)
        public const int NTSC_OLD_CYCLES_PER_LINE = 65;
        public const int NTSC_OLD_LINES = 263;
        public const int NTSC_OLD_LINES_VISIBLE = 235;                                             // TODO: Why is it 235? Shouldn't it be 263 - 30 = 233?
        //public const int NTSC_OLD_LINES_VISIBLE = NTSC_OLD_LINES - VBLANK_LINES;                 // 263 - 30 = 233. Total vertical lines excluding lines spent in VBLANK.
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
