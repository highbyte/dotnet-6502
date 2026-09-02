namespace Highbyte.DotNet6502.CycleEnginePrototype;

/// <summary>
/// Stand-in for the devices a C64 master clock would advance every cycle: a VIC-II that fetches
/// one byte per cycle and pulls BA low during bad-line cycles, and a CIA timer that raises an IRQ
/// on underflow. It exists so the engine comparison includes the cost shape of a per-cycle
/// scheduler, not only CPU dispatch. Timing constants follow the PAL VIC-II (63 cycles per line,
/// 312 lines, bad lines when the low three raster bits equal YSCROLL inside the display window,
/// BA low from cycle 12 to 54 of a bad line). Sprite DMA is not modeled.
///
/// <see cref="Tick"/> advances one cycle. <see cref="Advance"/> advances many cycles in closed
/// form (prefix sums for the fetches, arithmetic for the timer) and must produce exactly the state
/// the same number of ticks would: that is the "batch mathematically equivalent spans" rule the
/// lazily synchronizing engines rely on, and the equivalence tests check it.
/// </summary>
public sealed class SystemStub
{
    public const int CyclesPerLine = 63;
    public const int LinesPerFrame = 312;
    public const int CyclesPerFrame = CyclesPerLine * LinesPerFrame;
    public const int FirstBadLineCandidate = 0x30;
    public const int LastBadLineCandidate = 0xF7;
    public const int BaLowFromCycle = 12;
    public const int BaLowToCycle = 54;

    private readonly byte[] _vicRam = new byte[0x4000];
    private readonly long[] _fetchPrefix = new long[CyclesPerFrame + 1];   // sum of fetches for frame positions [0, t)
    private readonly CPUInterrupts _interrupts;
    private readonly InterruptSource _ciaIrq;
    private int _framePosition;     // RasterLine * CyclesPerLine + RasterCycle
    private int _ciaTimer;
    private int _yScroll = 3;
    private bool _badLinesEnabled = true;

    public SystemStub(CPUInterrupts interrupts)
    {
        _interrupts = interrupts;
        _ciaIrq = interrupts.GetSource("Stub.CIA.TimerA");
        for (var i = 0; i < _vicRam.Length; i++)
            _vicRam[i] = (byte)(i * 7);
        for (var t = 0; t < CyclesPerFrame; t++)
            _fetchPrefix[t + 1] = _fetchPrefix[t] + _vicRam[t & 0x3FFF];
    }

    /// <summary>Master cycles advanced so far.</summary>
    public ulong MasterCycle { get; private set; }

    public int RasterCycle => _framePosition % CyclesPerLine;
    public int RasterLine => _framePosition / CyclesPerLine;

    /// <summary>Bumped whenever something other than time changes the timing state; watermark caches key on it.</summary>
    public int StateVersion { get; private set; }

    public int YScroll
    {
        get => _yScroll;
        set { _yScroll = value; StateVersion++; }
    }

    public bool BadLinesEnabled
    {
        get => _badLinesEnabled;
        set { _badLinesEnabled = value; StateVersion++; }
    }

    /// <summary>True while the VIC-II holds BA low: CPU read cycles stall, writes proceed.</summary>
    public bool BaLow => IsBadLine(RasterLine) && RasterCycle >= BaLowFromCycle && RasterCycle < BaLowToCycle;

    /// <summary>Sum of the bytes the VIC-II fetched; keeps the fetch from being optimized away and lets tests compare device state.</summary>
    public long VicFetchAccumulator { get; private set; }

    public int CiaTimerLatch { get; set; } = 3000;
    public bool CiaTimerRunning { get; set; }
    public int CiaUnderflows { get; private set; }

    public void SetRasterPosition(int line, int cycle)
    {
        _framePosition = line * CyclesPerLine + cycle;
        StateVersion++;
    }

    public void StartCiaTimer(int latch)
    {
        CiaTimerLatch = latch;
        _ciaTimer = latch;
        CiaTimerRunning = true;
    }

    public bool IsBadLine(int line)
        => _badLinesEnabled && line >= FirstBadLineCandidate && line <= LastBadLineCandidate && (line & 7) == _yScroll;

    /// <summary>Advances every device by one master cycle (the phi1 half a CPU cycle starts with).</summary>
    public void Tick()
    {
        MasterCycle++;

        if (++_framePosition == CyclesPerFrame)
            _framePosition = 0;
        VicFetchAccumulator += _vicRam[_framePosition & 0x3FFF];

        if (CiaTimerRunning && --_ciaTimer < 0)
        {
            _ciaTimer = CiaTimerLatch;
            CiaUnderflows++;
            _interrupts.SetIRQActive(_ciaIrq, autoAcknowledge: true);
        }
    }

    /// <summary>Advances every device by <paramref name="cycles"/> master cycles in closed form.</summary>
    public void Advance(int cycles)
    {
        if (cycles <= 0)
            return;

        MasterCycle += (ulong)cycles;

        // VIC-II: fetches for frame positions (p, p + cycles], wrapping at the frame end.
        var from = _framePosition + 1;
        var to = _framePosition + cycles;           // inclusive
        var wholeFrames = 0;
        if (to >= CyclesPerFrame)
        {
            var beyond = to - CyclesPerFrame + 1;   // positions past the end of this frame
            wholeFrames = beyond / CyclesPerFrame;
            VicFetchAccumulator += _fetchPrefix[CyclesPerFrame] - _fetchPrefix[from];
            VicFetchAccumulator += wholeFrames * _fetchPrefix[CyclesPerFrame];
            VicFetchAccumulator += _fetchPrefix[beyond % CyclesPerFrame];
        }
        else
        {
            VicFetchAccumulator += _fetchPrefix[to + 1] - _fetchPrefix[from];
        }
        _framePosition = (_framePosition + cycles) % CyclesPerFrame;

        // CIA: n decrements; the first underflow comes after (_ciaTimer + 1) decrements, then every (latch + 1).
        if (CiaTimerRunning)
        {
            if (cycles <= _ciaTimer)
            {
                _ciaTimer -= cycles;
            }
            else
            {
                var period = CiaTimerLatch + 1;
                var afterFirst = cycles - (_ciaTimer + 1);
                CiaUnderflows += 1 + afterFirst / period;
                _ciaTimer = CiaTimerLatch - afterFirst % period;
                _interrupts.SetIRQActive(_ciaIrq, autoAcknowledge: true);
            }
        }
    }

    /// <summary>
    /// Master cycles until BA is next low, counted from the current position; 0 when it is low now.
    /// Closed form: the next bad line is the next line with the YSCROLL low bits inside the display
    /// window, wrapping to the next frame.
    /// </summary>
    public int CyclesUntilBaLow()
    {
        if (!_badLinesEnabled)
            return int.MaxValue;
        if (BaLow)
            return 0;

        var line = RasterLine;
        var cycle = RasterCycle;
        if (IsBadLine(line) && cycle + 1 < BaLowToCycle)
            return Math.Max(BaLowFromCycle, cycle + 1) - cycle;

        var next = NextBadLineAfter(line);
        var linesAhead = next > line ? next - line : LinesPerFrame - line + next;
        return (CyclesPerLine - cycle) + (linesAhead - 1) * CyclesPerLine + BaLowFromCycle;
    }

    private int NextBadLineAfter(int line)
    {
        var candidate = Math.Max(line + 1, FirstBadLineCandidate);
        var next = candidate + ((_yScroll - candidate) & 7);
        if (next > LastBadLineCandidate)
            next = FirstBadLineCandidate + ((_yScroll - FirstBadLineCandidate) & 7);
        return next;
    }
}
