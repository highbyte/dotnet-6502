# C64 emulation accuracy and known limitations

This page is for readers who want to know how close the C64 emulation is to the real machine at
the level of individual cycles, and where it deliberately or knowingly stops. For what the
emulation supports at all, see the [overview](overview.md); for how the device timing works, see
[Libraries](libraries.md#device-timing).

The emulation is checked against the [VICE test programs](https://sourceforge.net/p/vice-emu/code/HEAD/tree/testprogs/),
which run on a real C64 and report a pass or fail, or produce a picture that is compared with a
screenshot of real hardware. The programs named below are from that collection; the
[development guide](../../home/development.md#vic-ii-tests-from-the-vice-test-programs) explains how
to run them against the emulator.

## Models

- **C64 PAL** with the 6569 VIC-II and **C64 NTSC** with the 6567R8. The early NTSC chip (6567R56A,
  64 cycles per line) and the later 8565/8562 VIC-II revisions are not modelled; test programs
  written for those chips (the `_ntscold` builds, `lp-trigger/test2new`) differ.
- **CIA:** the original 6526. The 6526A ("new CIA") of later C64s differs in a few cycles of
  interrupt timing; it is not modelled and there is no option to select it. Test programs have a
  `new` variant for those cases.
- **SID:** see the [overview](overview.md#current-capabilities) for what each SID provider models.

## CPU (6510)

The CPU executes every documented instruction with its exact bus accesses and cycle counts, and
every undocumented NMOS opcode is available in the `FullUnofficial` compatibility profile. The C64's
default profile, `StableUnofficial`, has all of them except `LAS` ($BB, whose result depends on the
bus) and the `JAM` opcodes.
Interrupts follow the hardware's sampling rules, including an NMI taking over an IRQ or `BRK`
sequence already in progress.

Known limits:

- **`ANE` / `LXA` ($8B / $AB)** use the common "magic" constant `$EE` in every cycle. Real chips
  differ from one another, and some use a different constant in a cycle where the VIC-II holds the
  bus; VICE's `CPU/ane` programs, which check the constant under DMA, fail (they also fail on some
  real C64s).
- **The 6510 port's undriven bits** (bits 6–7, and bit 3 with no datasette attached) keep the value
  they were last driven to when switched to input. On the real chip that value fades after a few
  hundred milliseconds; here it does not fade.
- **`JAM` / `KIL`** halt the emulated CPU. The real chip keeps reading the bus in a fixed pattern
  while jammed; that is not reproduced.

## VIC-II

The default renderer follows the chip's graphics sequencer pixel by pixel, runs the bad line,
border and sprite DMA logic cycle by cycle, and holds the CPU off the bus in the cycles the chip
takes it. Raster interrupts, mid-line register changes, opened borders, FLD, linecrunch, DMA delay,
sprite multiplexing, sprite crunching and sprite stretching come out as on hardware.

Known limits:

- **Graphics memory changed in the cycles it is fetched.** The picture is drawn after each CPU
  instruction, so a program that writes graphics memory in the very cycles the chip reads it sees
  the change a few cycles early. Register changes are not affected: they are applied at the cycle of
  the write.
- **Sprite collisions** are latched as the raster line ends rather than at the pixel where the
  sprites meet. A collision register read in the middle of a line still sees the collisions up to
  where the beam is (see [Libraries](libraries.md)), but the collision *interrupt* is raised at the
  end of the line, later than on hardware. VICE's `irq-ackn-bug/irq-ack-vicii` measures this (its
  second half) and differs.
- **Sprite gap cases** (`spritegap/spritegap2`, `spritegap3`), where a sprite's data fetch and display
  interact at the edges of the sprite DMA window, differ.
- **Light pen** on the 8565 (`lp-trigger/test2new`) is not modelled; the 6569 behaviour is.

### The legacy pixel generator

The renderer's pixel generator can be switched to the *legacy* generator (the
`Vic2RasterizerPixelGeneratorType` option, also in the hosts' C64 settings). It is faster and is
kept for hosts where render time matters (browsers in particular), but it does not follow the
graphics sequencer:

- The character and bitmap graphics are drawn in 8-pixel blocks, and **XSCROLL, the display mode
  bits and the memory pointers are read once per raster line**. Changes to them in the middle of a
  line (mode splits, mid-line character set switches, the colour-fetch and FLI bugs) are shown from
  the next line on.
- **Sprites are drawn as whole 21-row bands**, one per time a sprite is displayed, with the shape,
  position and colours they had when the sprite started. Changing a sprite's data, X position,
  expansion, multicolour or priority while it is being shown (sprite splits, sprite crunch, sprite
  enable/disable mid-sprite) does not show.
- Border colour, background colours and the side border compares do follow the chip cycle by cycle,
  as with the default generator, and so does everything that is not drawing: interrupts, bad lines,
  sprite DMA stalls and sprite-to-sprite collisions are the same with either generator.
  **Sprite-to-background collisions** are taken from the drawn graphics with the default generator;
  with the legacy one they are computed separately from the screen memory and the scroll registers
  as they are at the end of each line, so mid-line changes do not affect them.

On VICE's VIC-II test programs the legacy generator reproduces about half the pictures the default
generator does; the difference is concentrated in the `dmadelay`, `spritesplit`, `spritecrunch`,
`spriteenable`, `spritedma`, `border`, `fldscroll`, `colorfetchbug`, `flibug` and `vsp-tester`
suites.

Turning off per-line sprites (the `Vic2RasterizerPerLineSprites` option) goes further: sprites are
drawn once at the end of the frame from the registers as they are then, so multiplexed sprites show
only their last position, and sprite collisions are checked once per frame.

## CIA (6526)

The timers follow the 6526's pipeline between a register write and the counter, cycle by cycle
(start, stop, force load, one-shot, the reload in the underflow cycle), and the interrupt control
register's flags, output and acknowledge are timed to the cycle, including the quirks of timer B
described in [Libraries](libraries.md#device-timing). The keyboard and joystick ports are modelled.

Not modelled:

- **Timer B counting timer A underflows (cascade)** and **counting the CNT pin**: timer B always counts
  system clocks, and timer A cannot count CNT. VICE's `CIA-AcountsB` and `ciavarious/cia6`–`cia9`,
  `cia14` and the Wolfgang Lorenz `cia1tab`, `cnto2` programs use these.
- **Timer output on port B (PB6 / PB7)**: the port bits do not pulse or toggle on underflow
  (`ciavarious/cia10`–`cia13`, `pb6pb7`, Lorenz `cia1pb6/7`, `cia2pb6/7`).
- **The serial shift register (SDR)**: the register holds what is written; nothing is shifted in or
  out and it raises no interrupt (`shiftregister/cia-sp-test-*`, `cia-sdr-*`).
- **The time-of-day clock**: its registers hold what is written; the clock does not run, and there
  is no alarm or read latch (`tod/*`, `ciavarious/cia15`).

Known timing differences:

- Writing a timer's high latch byte **loads the counter only while the timer is stopped**; one case
  where the real chip does not load it is not reproduced (Lorenz `loadth`).
- **Setting the one-shot bit** one cycle before an underflow must stop the timer at that underflow;
  here it does not stop it (Lorenz `flipos`).
- **Disabling timer A's interrupt** in the cycle its output would go active: a plain register write
  should still let the interrupt bit show, a read-modify-write instruction should not. The emulation
  follows the read-modify-write case for timer A (`dd0dtest` passes, `shiftregister/cia-icr-test2-*`
  do not); timer B follows the plain-write case.

## Other programs that differ

A few VICE programs fail for reasons not yet analysed and are listed here for completeness:
`interrupts/irqdma/test5`–`test7b` (interrupts during DMA) and the Wolfgang Lorenz `cputiming`
program. `interrupts/irqnoack/ackraster` depends on where in the KERNAL's timer period it is
started and passes or fails depending on that.
