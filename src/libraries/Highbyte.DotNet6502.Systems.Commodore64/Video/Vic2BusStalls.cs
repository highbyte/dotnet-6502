namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// The VIC-II's claim on the bus, as seen by the CPU. On a bad line the VIC-II fetches the 40
/// video matrix bytes during cycles 15-54 and pulls BA low three cycles earlier (cycle 12); for
/// every sprite with DMA on it fetches the sprite pointer and three data bytes in two cycles, BA
/// low three cycles ahead. The 6510 stalls on its first read while BA is low and resumes when BA
/// goes high, so a read that lands inside such a window waits until the window ends; writes are
/// never stalled. Windows are derived from the raster position of the read and the current VIC-II
/// registers.
///
/// <para>Sprite DMA comes from the VIC-II's per-sprite DMA state (<see cref="Vic2.SpriteDmaMask"/>),
/// which switches on at the Y compare and runs for the sprite's rows; the VIC-II is caught up to
/// the read's cycle first so that state is current. Approximations: the DEN bit is read live
/// instead of being latched during line $30; the bad-line condition does not model the
/// display-off idle state.</para>
///
/// <para>Cycle numbering follows the usual VIC-II documentation: cycle 1 is the first cycle of a
/// line. Internally 0-based offsets within the line are used.</para>
/// </summary>
internal sealed class Vic2BusStalls : IBusStallSource
{
    private const int BadLineBaLowOffset = 11;    // cycle 12
    private const int BadLineBaHighOffset = 54;   // cycle 55
    private const int BadLineFirstLine = 0x30;
    private const int BadLineLastLine = 0xF7;
    private const int SpriteBaLeadCycles = 3;
    private const int SpriteDmaCycles = 2;

    private readonly Vic2 _vic2;
    private readonly C64 _c64;
    private readonly int _cyclesPerLine;

    // Merged BA-low windows for one line, as 0-based offsets from the line start; ends exclusive.
    private readonly (int Start, int End)[] _windows = new (int, int)[16];
    private int _windowCount;

    public Vic2BusStalls(Vic2 vic2)
    {
        _vic2 = vic2;
        _c64 = vic2.C64;
        _cyclesPerLine = (int)vic2.Vic2Model.CyclesPerLine;
    }

    public ulong StallCyclesForRead(ulong busCycle, out ulong nextCheckBusCycle)
    {
        // Bring the VIC-II to the cycle this read occupies (busCycle - 1 cycles have completed), so
        // its raster line and sprite DMA state are those of the read.
        _vic2.CatchUpTo(busCycle - 1);
        var frameIndex = _vic2.CyclesConsumedCurrentVblank;

        var line = (int)(frameIndex / (ulong)_cyclesPerLine);
        var offset = (int)(frameIndex % (ulong)_cyclesPerLine);

        BuildWindows(line);

        for (var i = 0; i < _windowCount; i++)
        {
            var (start, end) = _windows[i];
            if (offset < start)
            {
                // Bus is free now; ask again when this window begins.
                nextCheckBusCycle = busCycle + (ulong)(start - offset);
                return 0;
            }
            if (offset < end)
            {
                var stall = (ulong)(end - offset);
                // The read completes at `end`; the next window (if any) begins after it.
                var nextStart = i + 1 < _windowCount ? _windows[i + 1].Start : _cyclesPerLine;
                nextCheckBusCycle = busCycle + stall + (ulong)(nextStart - end);

                // The VIC-II's own fetches happen while the CPU waits, before the CPU's next write.
                // Bring the VIC-II and the renderer through the stalled cycles now, so what the VIC-II
                // fetched (a bad line's video matrix row) reflects memory as it was, not as the
                // stalled instruction is about to leave it.
                _vic2.CatchUpTo(busCycle - 1 + stall);
                _c64.RenderProvider?.OnAfterInstruction();
                return stall;
            }
        }

        // No window left on this line: re-evaluate at the start of the next line.
        nextCheckBusCycle = busCycle + (ulong)(_cyclesPerLine - offset);
        return 0;
    }

    /// <summary>
    /// The BA-low windows that can cover cycles of the given line: the line's own bad-line window
    /// and sprite DMA (sprites 0-2 fetch at the end of the line, 3-7 at the start of the next), and
    /// the previous line's sprite 3-7 DMA that runs into this line. Windows beyond the line are
    /// kept (a stall may run past the line end); overlapping and adjacent windows are merged.
    /// </summary>
    private void BuildWindows(int line)
    {
        _windowCount = 0;

        var activePrevious = _vic2.SpriteDmaMaskPreviousLine;
        for (var n = 3; n < 8; n++)
            if ((activePrevious & (1 << n)) != 0)
                AddWindow(SpritePointerOffset(n) - _cyclesPerLine);

        if (IsBadLine(line))
            AddRange(BadLineBaLowOffset, BadLineBaHighOffset);

        var active = _vic2.SpriteDmaMask;
        for (var n = 0; n < 8; n++)
            if ((active & (1 << n)) != 0)
                AddWindow(SpritePointerOffset(n));

        SortAndMerge();
    }

    // 0-based offset (from the line start) of the sprite pointer fetch. Sprites 0-2 at the end of
    // the line (cycles CPL-5, CPL-3, CPL-1), sprites 3-7 at cycles 1, 3, 5, 7, 9 of the next line.
    private int SpritePointerOffset(int sprite)
        => sprite < 3
            ? _cyclesPerLine - 6 + 2 * sprite
            : _cyclesPerLine + 2 * (sprite - 3);

    private void AddWindow(int pointerOffset)
        => AddRange(pointerOffset - SpriteBaLeadCycles, pointerOffset + SpriteDmaCycles);

    private void AddRange(int start, int end)
    {
        if (end <= 0 || _windowCount == _windows.Length)
            return;
        _windows[_windowCount++] = (start, end);
    }

    private void SortAndMerge()
    {
        for (var i = 1; i < _windowCount; i++)
        {
            var w = _windows[i];
            var j = i - 1;
            while (j >= 0 && _windows[j].Start > w.Start)
            {
                _windows[j + 1] = _windows[j];
                j--;
            }
            _windows[j + 1] = w;
        }

        var merged = 0;
        for (var i = 0; i < _windowCount; i++)
        {
            if (merged > 0 && _windows[i].Start <= _windows[merged - 1].End)
            {
                if (_windows[i].End > _windows[merged - 1].End)
                    _windows[merged - 1].End = _windows[i].End;
            }
            else
            {
                _windows[merged++] = _windows[i];
            }
        }
        _windowCount = merged;
    }

    private bool IsBadLine(int line)
    {
        if (line < BadLineFirstLine || line > BadLineLastLine)
            return false;
        var control = _c64.ReadIOStorage(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER);
        var displayEnabled = (control & 0x10) != 0;
        return displayEnabled && (line & 7) == (control & 7);
    }
}
