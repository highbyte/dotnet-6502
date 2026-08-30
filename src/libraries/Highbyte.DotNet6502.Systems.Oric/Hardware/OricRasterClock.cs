namespace Highbyte.DotNet6502.Systems.Oric.Hardware;

/// <summary>
/// Advances the PAL Oric raster from CPU cycles and reports scanline, frame and optional
/// RGB-VSync-to-CB1 signal transitions from the same timing source.
/// </summary>
public sealed class OricRasterClock
{
    public event Action<int>? RasterLineStarted;
    public event Action? FrameCompleted;
    public event Action<bool>? VSyncLevelChanged;

    public int RasterLine { get; private set; }
    public int CycleInLine { get; private set; }
    public ulong FrameNumber { get; private set; }
    public int CycleInFrame => RasterLine * OricConfig.CyclesPerLine + CycleInLine;

    public void Advance(int cycles)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cycles);

        while (cycles > 0)
        {
            var cycleInFrame = CycleInFrame;
            var cyclesToLineEnd = OricConfig.CyclesPerLine - CycleInLine;
            var cyclesToVSyncTransition = FrameNumber == 0
                ? int.MaxValue
                : cycleInFrame switch
                {
                    < Oric.VSyncHackDelayCycles => Oric.VSyncHackDelayCycles - cycleInFrame,
                    < Oric.VSyncHackDelayCycles + Oric.VSyncHackLowCycles =>
                        Oric.VSyncHackDelayCycles + Oric.VSyncHackLowCycles - cycleInFrame,
                    _ => int.MaxValue,
                };
            var cyclesToAdvance = Math.Min(cycles, Math.Min(cyclesToLineEnd, cyclesToVSyncTransition));

            CycleInLine += cyclesToAdvance;
            cycles -= cyclesToAdvance;

            var newCycleInFrame = CycleInFrame;
            if (FrameNumber > 0 && newCycleInFrame == Oric.VSyncHackDelayCycles)
                VSyncLevelChanged?.Invoke(false);
            else if (FrameNumber > 0 && newCycleInFrame == Oric.VSyncHackDelayCycles + Oric.VSyncHackLowCycles)
                VSyncLevelChanged?.Invoke(true);

            if (CycleInLine != OricConfig.CyclesPerLine)
                continue;

            CycleInLine = 0;
            RasterLine++;
            if (RasterLine == OricConfig.LinesPerFrame)
            {
                RasterLine = 0;
                FrameNumber++;
                FrameCompleted?.Invoke();
                VSyncLevelChanged?.Invoke(true);
            }

            RasterLineStarted?.Invoke(RasterLine);
        }
    }

    public void Reset()
    {
        RasterLine = 0;
        CycleInLine = 0;
        FrameNumber = 0;
    }
}
