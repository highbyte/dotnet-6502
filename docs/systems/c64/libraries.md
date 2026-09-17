# Libraries used by the C64 system

## Core library

The C64 system logic — VIC2, CIA, SID, 1541 — lives in:

- [`Highbyte.DotNet6502.Systems.Commodore64`](../../libraries/system-specific/c64.md)
  · [source](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Systems.Commodore64)

This library has no UI, rendering, or I/O dependencies. It exposes abstractions that the implementation libraries below plug into.

### Device timing

The CPU executes one instruction at a time, but every cycle of it is a bus access counted by
`CPU.BusCycles`. The VIC-II and the two CIAs are brought up to date lazily from that counter: at
every instruction boundary, and at every access to one of their registers, to the cycle of the
access. A read of `$D012` therefore returns the raster line at the cycle the read happens, a CIA
timer read returns the count at that cycle, a raster-compare or timer-control write takes effect
on its own cycle. The CIA timers have the 6526's pipeline between a control write and the
counter: after a start the counter holds through the two cycles after the write and shows its
first decrement on the third; a force load shows the latch two cycles after the write and counts
from it two cycles later; a stop lets the counter move for two more cycles; a one-shot timer
stops with the latch in the counter; writing the latch's high byte while the timer is stopped
loads the counter, the low byte alone does not. The underflow flag shows in the interrupt control
register in the cycle the counter reads 0 for timer A and a cycle earlier for timer B (the
6526's timing; the 6526A's is not modelled), and the interrupt output follows in the next cycle
unless a read has taken the flag away. The CiaSyncedSplit sample in the Avalonia and browser menus
places a raster split with a CIA timer started in a known cycle, the classic timer stabiliser, and
marks where the split's edge belongs with that pipeline. A raster or CIA timer interrupt is dated to the cycle on
which the raster line began or the timer underflowed, and the CPU applies its sampling rule to
that cycle: taken after
the current instruction if it fell at or before the second-to-last cycle, otherwise after the
next one. The raster interrupt is raised when the raster compare goes from not matching to
matching, checked as the raster enters a line (a cycle later for line 0) and in the cycle after a
`$D011` or `$D012` write: writing the current line's number raises it at once, while a program
that moves the compare value to the next line in every line's last cycle keeps it matched and
gets no further interrupt. The light pen input, CIA 1's port B bit 4, latches the beam position on
a negative edge: `$D013` the X coordinate at the end of that cycle halved, `$D014` the raster line,
once per frame, with the light pen interrupt; programs pull it low to learn the cycle they are on.
The SID does the same through its audio provider (see below). The rasterizer applies a
write to the border or background colour registers (`$D020`-`$D024`) from the cycle after the
write lands, so a colour change in the middle of a line splits that line at the write's pixel
position, as on hardware; the VIC-II reports each register write with its frame cycle for this.
The other display registers (mode, scroll, 38/24-column, memory setup) are still sampled once per
raster line, so a mid-line change to one of those becomes visible on the next line. Where a cycle's
pixels land follows the chip: the display window's first pixel is 124 pixels into the raster line
on both PAL and NTSC, taken from the VIC-II's display window at X 24 and where X 0 falls relative to
the line's first cycle. The visible frame is the chip's own: the lines and pixels outside its
vertical and horizontal blanking, raster lines 16-299 and X 480-378 on the 6569, lines 41-12 and
X 489-394 on the 6567R8. So the border above the display window is smaller than the one below it
(35 and 49 lines on PAL, 10 and 25 on NTSC), and nothing from the blanking, where a sprite or an
opened border can still produce pixels, is shown. A colour register write is shown a few pixels away from the
cycle boundary it lands on, by an amount measured against VICE that is the same on PAL and NTSC. The
rasterizer does hold a character row's 40 screen codes and colour nibbles the way
the VIC-II does: fetched on the row's first line and shown for its remaining seven, so a screen
write made after that fetch appears from the next row on. When a CPU read is stalled, the VIC-II
and the renderer are brought through the stalled cycles before the CPU continues, so what the
VIC-II fetched during the stall reflects memory before the stalled instruction's write. A CPU write into the VIC-II's bank is likewise seen by the chip's fetches from the cycle after the write on, not from the instruction's end: the chip reads in a cycle's first clock phase and the CPU writes in its second, so a byte rewritten in the middle of a line reaches the screen from the column after the write's cycle (an idle byte changed mid-line, graphics rewritten as they are fetched). The VIC-II's bank follows the levels on CIA 2 port A's two bank pins, not the bytes written: a bit the direction register makes an input floats up through its pull-up, so a program selects a bank with `$DD02` as well as with `$DD00`.

The VIC-II also takes the bus from the CPU as on hardware: 40 cycles on every bad line (BA low
from cycle 12, video matrix fetches in cycles 15-54) and two cycles per sprite with DMA on, BA
low three cycles ahead. A CPU read that falls inside such a window waits until the window ends;
writes do not wait. Bad lines follow YSCROLL and the DEN bit as the VIC-II saw it during raster
line $30: clearing DEN before that line switches the display, and its bad lines, off for the whole
frame, clearing it later has no effect until the next frame, and setting it partway through line
$30 makes bad lines from the cycle after the write on (a write in the line's last cycle still
counts, one in the next line's first cycle does not). Sprites follow the chip's data
counters: the compare in cycles 55 and 56 of the line an enabled sprite's Y register names
switches its DMA on (on the 6567R8's 65-cycle line, cycles 56 and 57) and the display follows in cycle 58 (59), the row it fetches for each line comes
from a counter that takes the fetch's end position (three on) in cycle 16 of every line where the
Y-expansion flip-flop is set (inverted in cycle 55 while the expand bit is set, held set while it
is cleared), and the DMA ends once that counter has reached 63. So a Y-expanded sprite shows every
row twice, a change of the expand bit mid-sprite changes the line count from there on, a Y written
to a line the raster has already passed costs nothing until the raster comes round again, and a Y
rewritten below a finished sprite shows it again there. The display decision asks for the enable bit
again, as it stands in that cycle, so a sprite switched on for the compares and off before cycle 58
is fetched but not shown, and the display stays on until that decision finds the DMA off, so a
sprite whose Y is rewritten to its last line restarts there and shows its first row again on the
next line. A DMA the second compare starts leaves sprite 0's first data byte to the CPU (its fetch
is only two cycles on), so that byte reads $FF; a sprite 3-7 shown on the line its DMA starts, with
an X beyond cycle 58, carries what its slot read while the DMA was off: $FF, the idle byte, $FF.
The SpriteEnable sample in the Avalonia and browser menus shows the enable timing, the $FF byte
and the restart.
Clearing the expand bit in cycle 15 of a
line where the flip-flop is clear crunches the sprite: the fetch position becomes a bit-merge of
the counter and itself, the counter leaves its stride of three, misses 63 and wraps, and the
sprite's rows come out reordered and its DMA runs on for up to 63 rows, as the demos use it. With per-line sprites on, the rasterizer's
sequencer generator draws each raster line's sprites from what the chip fetched for it, so sprite
pointer and data changes mid-sprite show as on hardware; the legacy generator keeps its per-sprite
bands. Where a sprite appears on a line follows the chip's X compare per pixel: a sprite starts
shifting its row out where its X register equals the beam position, an X written in a cycle counts
from that cycle's fifth pixel, and a sprite whose X is moved past the beam after it was shown
starts again, so one sprite can appear twice on a line. It cannot start while its own data is
being fetched (cycles 58 and 59 for sprite 0, two cycles on per sprite, so X 355 to 366 on the
6569); one still shifting when its fetch begins repeats its last pixel for seven pixels and stops;
and the fetch loads the next row, which can start on the same line, so a sprite whose X lies beyond
its fetch shows each row a line higher. An X the beam never reaches (504 to 511 on the 6569) is
never shown. The sprite's multicolour, X-expand and priority bits are read at every pixel too, so a
change while a sprite shifts takes effect from that pixel: setting X-expand repeats the pixel being
shown and clearing it fetches the next at once, switching multicolour on or off realigns the pixel
pairs on the shape's odd bits (the sprite split effect), a sprite can be in front of the graphics on
one part of a line and behind them on the rest, and a sprite colour written mid-line changes from
that pixel like the background colours do. Sprite-to-sprite collisions come from these runs and are latched as the line ends, and so are
sprite-to-background collisions, from the runs' opaque pixels against the foreground pixels the
graphics sequencer output on the line (a set bit, or a 10/11 pair in multicolour; nothing while
the vertical border flip-flop is set). The collision interrupt flags latch whether or not the
source is enabled, and reading a collision register clears the collisions, not the flag. Where
sprites overlap, the lowest-numbered one with an opaque pixel is shown and its own priority bit
alone decides against the foreground graphics, so a sprite in front of the graphics does not show
through a higher-priority sprite that is behind them. The
SpriteX sample in the Avalonia and browser menus shows each of these cases.

Which character row a raster line shows, and which of its eight lines, is not arithmetic on the
line number but the chip's own display state: a bad line starts a row (the row counter resets and
the row is fetched), the eighth line of a row advances the row pointer by 40 and drops the chip
into idle state until the next bad line, and in idle state the display area shows the byte at
`$3FFF` (`$39FF` with ECM) in black over the background colour. The VIC-II keeps that state per
line for the legacy pixel generator and the bus stalls; the sequencer pixel generator runs the
chip's counters cycle by cycle after the VIC-II article's rules: the bad line condition (raster
line $30-$F7, its low three bits equal to YSCROLL as it is in that cycle, DEN seen in a cycle of
line $30 up to the one the write lands in) is evaluated every cycle, cycle 14 loads the video
counter and resets the row counter on a condition, a condition in any cycle puts the sequencer in
display state from the cycle after it (the g-access of the cycle itself is still an idle one, and
reads `$38FF` rather than `$3FFF`, as the 6569 and 6567R8 do) and one in cycles 12-54 starts the
video matrix fetches, whose first three read $FF while the CPU still holds the bus, and cycle 58
ends a row after its eighth line unless a condition keeps the display going. A DEN bit set
partway through line $30 therefore decides between a normal row, a row whose counter reset was
missed and a row shifted by a column, by the cycle the write lands in. That is what
makes the DMA delay (a bad line condition created in the middle of a line shifts the screen right
by a column per cycle), linecrunch, doubled rows and FLD come out as on hardware. Running every
cycle of every line makes an opened border, where the whole line is output, about twice the work
of a text screen, so the per-cycle path is kept cheap: a cycle whose fetched bytes, mode and
pipeline state equal the cycle before repeats that block's pixels by copy (runs of identical
cells, the idle byte across an opened border, blank cells under any XSCROLL), the line's colour
resolve copies such blocks as well, and sprite rows are written as runs of pixels rather than a
pixel at a time. The vertical border compares are
checked in every cycle with the registers as they are then: the top compare (line 51, or 55 with
RSEL clear) with DEN set clears the vertical border flip-flop at once, and the bottom compare (251
or 247) arms a latch that the flip-flop takes over as the raster enters a line and at the display
window's left edge; while the flip-flop is set the whole line, sprites included, is border colour.
That is what makes vertical fine scrolling, a switched-off screen, a border opened by toggling
RSEL around line 251 and row stretching by avoiding bad lines come out as on hardware, and it is
why RSEL cleared for a few cycles in line 247 closes the border at that line's left edge or, if
later in the line, from the next line. A `$D011` write is seen from the cycle after it, so one
early enough in a line (before the left edge check in cycle 16 and the bad line check at cycle 14)
still counts for that line, and one in a line's last cycle counts for the next.

The side borders come from the chip's main border flip-flop, followed pixel by pixel: it is set
when the X coordinate reaches the right compare value (344 with 40 columns, 335 with 38) and reset
at the left one (24 or 31) on a line the vertical flip-flop leaves open, and a pixel is border
colour while it is set. Each compare is evaluated in the cycle after the one its X coordinate
falls in, with the registers as written before that cycle: the 38 column right compare in cycle
56 (counting from 1) and the 40 column one in 57. The 40 and 38 column layouts are what those
rules give on an ordinary line; a program that selects 38 columns with a write in cycle 56 misses
both compares, and the display then runs to the frame's edges on that line and the left border of
the next, with sprites visible there. The column select bit follows the register write journal at the
cycle boundary after the write. An opened border shows the sequencer's idle output, the byte at
`$3FFF` in black over the background colour.

The graphics themselves come from the chip's graphics data sequencer, followed pixel by pixel as
the VIC-II article describes it, with the points within a cycle at which register changes reach the
output taken from the VICE project's emulation and test programs, which the result is checked
against. Each cycle's g-access (the chip's cycles 16-55, one per column) puts a byte into
a two-stage pipeline, and the shift register takes it at the pixel XSCROLL selects in the cycle
after that, together with the video matrix byte and colour nibble that belong to it; in idle state
the byte comes from `$3FFF` (`$39FF` with ECM) with no matrix data, and outside those cycles the
pipeline is fed zeros. XSCROLL, the mode bits and the memory pointers reach the sequencer through
the register write journal, from the cycle after the write: XSCROLL moves the next load within its
cycle (the old byte's zeros show until then), MCM takes effect four pixels into the cycle, ECM and
BMM four pixels in when set and six when cleared, and the pointers apply to the next g-access. Each
pixel's two bits select its colour source from the mode table (a background colour register, the
matrix nibbles, the colour nibble, or black in the invalid modes) and its priority, and the line's
pixels are resolved into the two layers when the line ends, with the background colour registers'
values at each pixel. That is what makes mid-line mode, scroll and character set switches, and the
opened-border and idle pictures of VICE's border and videomode tests, come out as on hardware. One
limit remains: the rasterizer runs after each instruction, so a program that changes graphics
memory in the very cycles the chip fetches it sees the change a few cycles early. The sequencer
costs about a tenth more render time per frame than the generator it replaced, so that generator
(8-pixel blocks, the display registers sampled once per line) is kept as the legacy pixel
generator, selectable with the `Vic2RasterizerPixelGeneratorType` configuration option and in the
hosts' C64 settings, for browsers and other hosts where that matters. It receives no new features. Sprite X positions wrap at 512 as on the chip, so a sprite at X 496 sits in the left
border. Sprite DMA takes the bus one cycle later on the 6567R8 than on the 6569 (its sprite 0
pointer fetch is in cycle 59 rather than 58), which is what a program timed by that hold sees.

## Implementation libraries

C64-specific host code lives in its own **engine-plugin** libraries, one per host technology,
named `Highbyte.DotNet6502.Impl.<Tech>.Commodore64`. Each carries the C64 render targets for that
host technology (where one exists), C64 host config, and an `ISystemEnginePlugin` that registers
the C64 with the host app's DI container. Host apps **discover these plugins at runtime** — see
[`Highbyte.DotNet6502.Systems.Plugins`](../../libraries/core/dotnet6502-systems-plugins.md) — and
hold no direct project reference to them.

| Engine-plugin library | Host technology | Used by app |
| --------------------- | --------------- | ----------- |
| `Highbyte.DotNet6502.Impl.Skia.Commodore64` | SkiaSharp | Blazor WASM, SilkNetNative |
| `Highbyte.DotNet6502.Impl.SilkNet.Commodore64` | OpenGL shaders via Silk.NET | SilkNetNative |
| `Highbyte.DotNet6502.Impl.SadConsole.Commodore64` | SadConsole | SadConsole |
| `Highbyte.DotNet6502.Impl.Avalonia.Commodore64` | Avalonia | Avalonia Desktop, Avalonia Browser |
| `Highbyte.DotNet6502.Impl.AspNet.Commodore64` | Blazor / JS interop | Blazor WASM |
| `Highbyte.DotNet6502.Impl.Headless.Commodore64` | none (headless) | Headless |
| `Highbyte.DotNet6502.Impl.Terminal.Commodore64` | Terminal.Gui (text cells) | Terminal (TUI) |

### Render

C64 render targets live under `Commodore64/Render/` in the engine-plugin libraries above
(`Impl.Skia.Commodore64`, `Impl.SilkNet.Commodore64`, `Impl.SadConsole.Commodore64`). The Avalonia
desktop and browser apps render the C64 via the generic Avalonia bitmap render target in
[`Highbyte.DotNet6502.Impl.Avalonia`](../../libraries/implementation/avalonia.md) — there is no
bespoke C64 renderer, so `Impl.Avalonia.Commodore64` exists only for engine registration and host
config. Likewise the Terminal (TUI) app renders the C64 (character mode only) via the generic
terminal render target in [`Highbyte.DotNet6502.Impl.Terminal`](../../libraries/implementation/terminal.md),
so `Impl.Terminal.Commodore64` exists only for engine registration and host config.

### Input

C64 keyboard handling is **no longer per host**. One reusable `C64InputHandler` (with
`C64HostKeyboard` / `C64InputConfig`) lives in the C64 system core
[`Highbyte.DotNet6502.Systems.Commodore64`](../../libraries/system-specific/c64.md) under `Input/`;
each host only supplies a small native-key → `HostKey` translation table inside its own input
context. A few genuinely host-specific bits remain in the engine-plugin libraries (for example
`C64SilkNetGamepad` in `Impl.SilkNet.Commodore64`).

### Audio

C64 audio is host-agnostic. The C64 system declares two interchangeable audio providers; the
host app's audio target chain consumes whichever one is currently selected via the C64 config
UI (`Audio provider` / `Audio target` / `SID emulation` combos).

| Provider | Default | Accuracy | CPU | Notes |
| --- | --- | --- | --- | --- |
| **Sample-based** (`C64SidSampleProvider`) | yes | Good but not perfect | Higher | Pure-managed sample-accurate SID emulation. All four waveforms (individual and combined via bitwise AND), full ADSR with the real 16 rate-counter periods, hard sync, ring modulation, TEST-bit hold, OSC3/ENV3 readback, a generic resonant 2-pole state-variable filter (LP / BP / HP), and the `$D418` volume DAC's audible DC term so digi / sample-playback tunes should work. Inner loop takes auto fast paths when the current SID state doesn't actively use the advanced features. Default output rate is 48 kHz, with integer Bresenham downsampling from the SID clock. Register writes are applied on the exact CPU cycle they happen (the core is caught up to the write's bus cycle first), OSC3/ENV3 reads see the state at the cycle of the read, and the `$D418` volume DAC is averaged over each output sample's window, so high-rate `$D418` sample playback has neither instruction-boundary jitter nor sample-point aliasing. Reads of write-only registers return the chip's data-bus latch (the last byte written to or read from the SID) until it decays after about 7,400 cycles, as on a 6581, so a loader that does `DEC $D418` for its loading noise counts the volume down and buzzes like the real machine. Missing: chip-variant filter models (6581 R1/R2/R3/R4 vs 8580), chip-measured combined-waveform tables, and anti-aliased downsampling. |
| **Command stream** (`C64SidCommandStream`) | no | Not very accurate | Lower | Legacy. Decodes SID register changes into host-agnostic synth commands (volume, voice ADSR + oscillator). A host-side oscillator graph (NAudio or WebAudio) turns them into sound. Cannot reproduce the SID filter, combined waveforms, ring modulation, hard sync, or digi / sample playback. |

The sample-based provider has two **SID emulation modes** (selectable in the config UI):

- `Auto` (default) — full accuracy as listed above.
- `Fast` — drops the advanced features (single waveform per voice, no sync / ring mod / TEST
  hold / OSC3/ENV3 readback / filter). Modest savings (~4% per frame) on sync-using tunes,
  near zero on simple tunes. Many tunes will sound wrong.

Each provider is paired with a host audio target that knows how to play its output style:

| Provider | Compatible target on desktop | Compatible target in browser |
| --- | --- | --- |
| Sample-based | `NAudioSampleTarget` ([`Impl.NAudio`](../../libraries/implementation/naudio.md), playback via `OpenAL`) | `NAudioSampleTarget` ([`Impl.NAudio`](../../libraries/implementation/naudio.md), playback via WebAudio JS interop) — Avalonia Browser only |
| Command stream | `NAudioCommandTarget` ([`Impl.NAudio`](../../libraries/implementation/naudio.md), playback via `OpenAL`) | `NAudioCommandTarget` ([`Impl.NAudio`](../../libraries/implementation/naudio.md), playback via WebAudio JS interop) on Avalonia Browser; `WebAudioCommandTarget` ([`Impl.AspNet`](../../libraries/implementation/aspnet.md), direct WebAudio oscillator nodes) on Blazor WASM |

**Per-app availability:**

- **Avalonia Desktop**, **SadConsole**, **SilkNetNative**: both providers available (sample-based by default).
- **Avalonia Browser**: both providers available (sample-based by default), playback via WebAudio JS interop.
- **Blazor WASM**: command-stream only. The sample-based provider is not yet wired up here — see the design log for the planned work.
- **Terminal (TUI)**: no audio (terminals have no audio output).

There is no C64-specific audio library — the former `Impl.NAudio.Commodore64` was removed
when the audio command vocabulary was generalised, and the new sample path is also
system-agnostic on the target side.

For the cross-system view (which app uses which library, including Generic), see the [Implementation libraries overview](../../libraries/implementation/overview.md).
