# Overview of the Apple II system

A minimal implementation of an Apple II Plus.

Core library: `Highbyte.DotNet6502.Systems.Apple2`.

The system's single configuration variant is named `APPLE2PLUS` after the emulated machine
model, following the C64 precedent (`C64NTSC`/`C64PAL`). Future family models that share the
hardware core (the original Integer BASIC Apple II, a PAL Europlus) would be added as further
variants; only a fundamentally different machine (e.g. the 65C816-based IIgs) would warrant a
separate system.

The Apple II is the simplest machine in this project. It has no timer chip and no interrupt
source at all — the Autostart Monitor and Applesoft BASIC poll the keyboard latch directly — so
there is no CIA/VIA equivalent to emulate and no keyboard matrix to scan.

## Current capabilities

- Boot to the Applesoft BASIC `]` prompt from a user-supplied Apple II Plus ROM.
- 48 KB flat RAM at `$0000`–`$BFFF`, no bank switching.
- 12 KB system ROM (Applesoft BASIC + Autostart Monitor) at `$D000`–`$FFFF`.
    - Accepts both a trimmed 12 KB image and the 20 KB `$B000`–`$FFFF` layout that older
      emulator distributions use (the loadable part is the last 12 KB).
- Soft switches at `$C000`–`$C0FF`, decoded on bits 7-4 so each switch covers 16 addresses:
    - `$C000` keyboard data (ASCII in bits 6-0) + strobe (bit 7).
    - `$C010` clears the strobe.
    - `$C050`–`$C057` display-mode switches (text/graphics, mixed, page 1/2, lo-res/hi-res),
      honored by the rasterizer render path.
    - `$C030` speaker toggle, driving the one-bit cone (see Speaker audio below).
- Empty peripheral slots at `$C100`–`$CFFF` read as `$FF`, which makes the Autostart ROM's disk
  scan fail cleanly so it falls through to BASIC. Slot 6 answers instead when the Disk II
  controller is active (see below).
- Disk II controller card in slot 6, read-only: boots and runs standard 16-sector DOS 3.3
  disk images (`.dsk`/`.do`). See [Disk II](disk2.md).
- 40 &times; 24 text display in the hardware's 7&times;8 character cell (280&times;192 pixels),
  page 1 (`$0400`–`$07FF`) or page 2 (`$0800`–`$0BFF`).
    - Interleaved row addressing: `address = base + (row % 8) * $80 + (row / 8) * $28`.
    - The 8 "screen hole" bytes per 128-byte block are firmware scratch space and are never
      displayed.
    - Normal (`$80`–`$FF`), inverse (`$00`–`$3F`) and flashing (`$40`–`$7F`) video; flashing
      alternates at roughly 2 Hz.
    - Uppercase-only 64-glyph character set, matching the Apple II / II Plus character generator.
- Lo-res graphics (GR): the active text page reinterpreted as 40&times;48 colour blocks (two
  stacked 4-bit colours per screen byte), rendered with the 16-colour lo-res palette on a colour
  monitor. A phosphor monitor has no chroma to show, so the same blocks arrive as shades of its
  phosphor — which means colours of equal brightness become indistinguishable, exactly as on the
  hardware.
- Hi-res graphics (HGR): 280&times;192 from page 1 (`$2000`) or page 2 (`$4000`), with the same
  interleaved line addressing as real hardware
  (`offset = (y % 8) * $400 + ((y / 8) % 8) * $80 + (y / 64) * $28`).
- Hi-res NTSC artifact colour on a colour monitor: an isolated lit dot takes its colour from the
  parity of its column across the scan line (violet or green) shifted by bit 7 of its byte (blue
  or orange), and a dot with a lit neighbour reads as white. These are the six colours Applesoft
  exposes through `HCOLOR`. The unit of colour is the two-dot colour cycle, so one lit dot tints
  both of its columns and colour resolution is 140 across rather than 280 — as on the hardware,
  where the monitor cannot resolve the dots inside a cycle. On the phosphor monitor settings the
  same dots render as the plain monochrome pattern at the full 280 instead.
- Mixed mode: graphics with the bottom 4 text rows, for both lo-res and hi-res.
- Game port: the three pushbuttons at `$C061`-`$C063` and the analog paddles at `$C064`-`$C067`,
  fired by the `$C070` strobe. There is no digital joystick on this machine — a stick is two
  potentiometers, so a gamepad direction drives its axis to the end of its travel and `PDL(n)`
  reads it back. Both stick buttons are wired — games do use the second, e.g. Choplifter turns the
  helicopter with it. A host gamepad works out of the box (A and B are the two buttons); the
  keyboard can drive it too, enabled by "Enable keyboard joystick controls" in the configuration
  dialog, which is the only place the setting is saved. The "Joystick KB"
  checkbox in the sidebar shows and changes that same setting without saving it — handy for trying
  it mid-game — and takes effect immediately on a running machine. Entries in "Download &amp; Run
  programs" can switch it on for themselves, as the C64 list does, so a title that needs the game
  port arrives playable: `W`/`A`/`S`/`D` steer, Space is
  button 0 and Left Shift is button 1. WASD rather than the arrows so the Apple II's own arrow keys
  keep working, and Left Shift so Right Shift is still free for typing shifted characters.
- Speaker audio. The machine has no sound chip: `$C030` toggles a one-bit cone, and every sound it
  makes — beeps, game effects, digitised speech — is software flipping that bit at chosen moments.
  Reproducing it is therefore resampling rather than synthesis: the cone's level is averaged over
  each output sample, which is what makes pulse-width modulation (how the Apple II fakes
  intermediate levels, and plays sampled audio at all) come out as sound rather than aliasing
  noise. A DC blocker removes the offset of a cone left parked on one side. Off by default;
  enable it in the configuration dialog.
- Selectable monitor: a composite colour monitor (the default) or a green, white or amber
  phosphor monitor. The choice applies to every mode, not just hi-res: text is monochrome either
  way (a colour monitor renders it white), and lo-res is full colour on a colour monitor and
  phosphor-tinted luminance on a monochrome one.
- Pixel-exact render path (`Apple2Rasterizer`, the default): draws text cells from the real 5&times;7
  dot patterns in the character generator ROM and all graphics modes, in two compositing layers.
- Lightweight glyph command-stream render path (`Apple2VideoCommandStream`) for hosts that draw
  characters rather than pixels. It renders on an 8-pixel grid using a host font, so on a host
  with fixed 8&times;8 cells it overflows the 280-pixel display slightly — and its glyph command
  vocabulary cannot express pixel graphics, so it always shows the text page. Prefer the
  rasterizer for a faithful picture and for graphics modes.
- Host-agnostic input handling — host key to ASCII with Shift and Control, plus typematic
  auto-repeat.
- **Host keyboard layout detection**, so punctuation matches the keys the user is actually
  pressing. **US English** and **Swedish** are supported, and anything else falls back to US.
  The layout is resolved once when the input handler starts, in this order: the explicit
  *Keyboard layout* setting in the Apple II configuration dialog → the host's active keyboard
  layout, read from the OS → the OS culture → US. Leaving the setting on **Auto** (the default)
  runs the whole chain; every step is logged, so the Log tab shows which layout was chosen and
  why. Notes on the Swedish map:
    - Å, Ä and Ö have no 7-bit ASCII form, so those three keys are bound to `[`, `]` and `\`
      (shifted: `{`, `}`, `|`) — characters a Swedish keyboard can otherwise only reach through
      Alt/Option chords that differ between macOS and Windows.
    - `Alt`/`Option` + `2`, `4`, `8`, `9` give `@`, `$`, `[`, `]`. Only these four are bound,
      because they are the chords macOS and Windows agree on.
    - `^` (Applesoft's exponent operator) is on shifted `¨`.
    - On macOS the two ISO keys `§` and `<` arrive with their hardware codes swapped relative to
      the W3C convention; this is corrected automatically for non-US layouts.
- CTRL-RESET (warm reset through the reset vector, like the RESET key on the real keyboard)
  on **Ctrl + F12**, the same combo the Virtual ][ emulator uses.
- Program loading without disk emulation, via the Avalonia sidebar's Load/Save section and
  the machine code monitor:
    - Applesoft BASIC files as bare tokenized bytes (no header, always at `$0801`); after
      loading, the Applesoft zero-page pointers are initialised so RUN and LIST work.
      Save Basic exports the same format.
    - Binary files in the DOS 3.3 "B" layout (4-byte header: load address + length), started
      BRUN-style at the load address.
    - Bundled example programs (one Basic, one ca65 assembly) loadable directly from the
      sidebar; sources in the repository's `samples/` folder.
- DOS 3.3 `.dsk`/`.do` disk images as a **file source**, separate from the drive: the
  Avalonia sidebar's Load/Save section can open an image, list its catalog, and load + run an
  Applesoft (A) or Binary (B) file — B files BRUN-style, A files at `$0801` with pointer init
  and an automatic `RUN`. This path injects into RAM with no drive present, so programs that
  read the disk at runtime (DOS calls, level streaming) have to be booted instead.
- A curated **"Download &amp; Run programs"** section (with download caching) that covers both:
  each entry declares whether it is injected into RAM or booted in the [Disk II](disk2.md)
  drive, so RAM-resident titles and self-booting ones sit in the same list. Booting entries
  need the optional `disk2` ROM.
- Copy/Paste of Applesoft BASIC in the Avalonia sidebar: **Copy** detokenizes the program in
  memory to source text on the clipboard; **Paste** types clipboard text into the machine via
  the keyboard latch (paced by consumption, letters typed as uppercase).
- Remote control support: the generic `keyboard.press`/`release` commands work through the
  system's input injector; `apple2.type`, `apple2.isbasicstarted` and `apple2.getbasicsource`
  mirror the C64's typing/BASIC-source commands; and `apple2.insertdisk`, `apple2.bootdisk`,
  `apple2.ejectdisk` and `apple2.diskstatus` drive the Disk II — see
  [TCP protocol](../../tools/remote-control/tcp-protocol.md).

- Avalonia Desktop and Avalonia Browser (WASM) UI, with a configuration dialog for ROM files,
  ROM download, monitor colour, render provider and CPU compatibility profile.
- Emulator state snapshots — save and restore the whole machine, disk included. See
  [Snapshots](#snapshots).

## Snapshots

The Apple II supports the shared `.d6502snap` emulator-state snapshots, so the same save/load
buttons, `emu.savesnapshot`/`emu.loadsnapshot` remote commands, Lua `emu.save_snapshot`, and
`--load-snapshot` command line work here as on the other machines.

Two machine modules make up an Apple II snapshot, alongside the shared `cpu-6502` one:

| Module | What it holds |
|---|---|
| `apple2-core` | The 48 KB RAM, the keyboard latch (strobe included), the four display soft switches, the speaker's cone position, and the game port's paddle positions, buttons and one-shot |
| `apple2-disk2` | Head half-track, motor, drive select, the Q6/Q7 latches, the read head's position in the current track — plus the inserted `.dsk` image, embedded in the package |

One module rather than one per chip, because the machine has no chips to divide along: the
display mode is four write-only flip-flops, the keyboard is one byte and the speaker is one bit,
all decoded from the same page of addresses. The Disk II card is the other seam, because it has
removable media.

Notes on what a restored machine gets:

- **The disk comes with it.** The image is embedded in the snapshot, so a snapshot of a booted
  DOS 3.3 session restores complete — no need to have the original file still lying around. The
  head position travels too, which matters more here than on a C64: this card has no processor of
  its own, so a running RWTS resumes against a head that is exactly where it left it.
- **Timing is continuous.** The disk motor's spin-down, the paddle one-shots and the speaker all
  time themselves against the CPU's cumulative cycle count, which the shared `cpu-6502` module
  restores. A paddle read caught in flight resumes and expires at the same point it would have.
- **Settings travel optionally.** Audio enabled, keyboard joystick and monitor colour are carried
  in the snapshot's portable settings block, applied before the machine is rebuilt (all three are
  read at build time rather than polled), and only when the user opts into including config.
- **ROMs are not captured**, by design — the system ROM, character generator and Disk II boot ROM
  come from the configuration when the machine is rebuilt. Restoring a snapshot taken with a disk
  inserted on a machine with no `disk2` ROM configured re-inserts the disk but warns that the
  controller stays invisible.

## Monitor commands

Additional machine code monitor commands specific to the Apple II system:

```
Commands:
  lb     Apple II - Load a tokenized Applesoft BASIC file (no header) from file picker dialog.
  llb    Apple II - Load a tokenized Applesoft BASIC file (no header) from host file system.
  sb     Apple II - Save a tokenized Applesoft BASIC file (no header) to host file system.
```

For general monitor commands, see [Monitor library](../../libraries/core/dotnet6502-monitor.md).

## Not yet implemented

- NTSC-accurate hi-res colour. The six artifact colours are modelled, but the underlying
  half-dot shift is not: bit 7 selects a colour rather than moving the dots half a pixel, so
  colour fringing at black/white boundaries does not appear.
- Disk II writing (the drive is always write-protected), a second drive, ProDOS-ordered `.po`
  images, and nibble/flux (`.nib`/`.woz`) media. See [Disk II](disk2.md) for what is supported.
- Sound cards (Mockingboard and friends). The built-in speaker is emulated; expansion audio is not.
- Peripheral slots other than slot 6, cassette, the second paddle pair (2 and 3), light pen.
- The language card / 16 KB RAM expansion.
- The original, non-Autostart Apple II with Integer BASIC. That is a different ROM set rather
  than different hardware, so it is a plausible later configuration variant.

## ROMs

See [ROMs](roms.md).
