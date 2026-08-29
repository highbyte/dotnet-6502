# Overview of the Oric system

A partial implementation of the **Oric Atmos 48K** (`ATMOS48K`).

Core library: `Highbyte.DotNet6502.Systems.Oric`.

## Current capabilities

- NMOS 6502 at 1 MHz with PAL timing: 64 CPU cycles per line, 312 lines and 19,968 cycles per frame.
- 48 KB RAM, the MOS 6522 at `$0300`-`$03FF`, and a 16 KB system ROM at `$C000`-`$FFFF`.
- 6522 parallel ports, control pins, timers and IRQ handling.
- AY-3-8912 register access through the VIA control pins and mono PCM audio for its three tone,
  noise and envelope channels.
- The Atmos eight-by-eight keyboard matrix. Host arrow, modifier, editing, alphanumeric and
  punctuation keys map to their physical Oric keys. PC Alt and Mac Option map to the Atmos
  **FUNCT** key; PC Backspace and Mac Delete map to **DEL**. **F12** remains the emulator
  machine-code monitor shortcut. Atmos BASIC control combinations such as **Ctrl+T** for
  CAPS/lowercase and **Ctrl+C** to interrupt a program use the host Control key.
- Copying the tokenized Atmos BASIC 1.1 program as source text and pasting clipboard text through
  the ROM keyboard input path. BASIC keywords must be uppercase, as on the real machine.
- Loading BASIC programs from byte-level Oric `.tap` files directly into RAM, including Atmos
  BASIC pointer initialization and embedded examples for text, hires graphics, sound effects,
  three-voice music, and AY tone/noise/envelope control. Multi-file tapes and cassette signal
  timing are outside this direct-loading path.
- The 240 &times; 224 active display in text and 240 &times; 200 hires modes, including serial
  ink, paper, character and screen attributes, alternate character sets, inverse and flashing
  characters, and the three text rows below hires graphics.
- Avalonia Desktop and Avalonia Browser integration, with ROM and audio configuration.

## Not yet implemented

- Cassette signal input/output and tape transport emulation.
- Microdisc or Jasmin floppy controllers and disk images.
- Printer output, joystick interfaces, snapshots and Oric-specific monitor commands.
- Cycle-level ULA bus contention and analogue AY output filtering.
- The Oric-1, Oric-1 16K, Pravetz and Telestrat variants.

## ROMs

The Atmos needs one copyrighted 16 KB firmware image. See [ROMs](roms.md) for the accepted image,
checksum, storage locations and the download acknowledgement.
