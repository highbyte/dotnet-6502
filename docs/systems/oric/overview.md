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
  punctuation keys map to their physical Oric keys. US and Swedish host layouts are supported,
  with automatic operating-system detection or an explicit selection in the Avalonia and Terminal configs.
  Swedish Å/Ä/Ö provide convenient access to the Atmos `[`, `]` and `\` keys, while common
  Swedish Alt/Option/AltGr symbol chords are translated to the corresponding Atmos keys.
  PC Alt and Mac Option map to the Atmos
  **FUNCT** key; PC Backspace and Mac Delete map to **DEL**. **F12** remains the emulator
  machine-code monitor shortcut. Atmos BASIC control combinations such as **Ctrl+T** for
  CAPS/lowercase and **Ctrl+C** to interrupt a program use the host Control key.
- Copying the tokenized Atmos BASIC 1.1 program as source text and pasting clipboard text through
  the ROM keyboard input path. BASIC keywords must be uppercase, as on the real machine.
- Parsing multi-file byte-level Oric `.tap` images and directly loading BASIC or machine-code
  records into RAM, including load-address and auto-run handling. A virtual tape cursor also feeds
  standard Atmos ROM `CLOAD` calls in sequence, so programs can load later records from the same
  tape. The Avalonia tape section can attach or replace an image without loading it, eject or rewind
  it, show the byte position and current/next file, and move safely to the previous or next parsed
  file boundary. Embedded BASIC examples cover text, hires graphics, sound effects, three-voice
  music, and AY tone/noise/envelope control.
- Avalonia **Download & Run** support for a curated set of Oric programs hosted by Oric.org. TAP
  images may be downloaded directly or extracted from a named entry in a ZIP archive, are cached
  by the host application, and are started through the standard Atmos ROM cassette routine. The
  Browser host routes these downloads through its configured CORS proxy.
- PASE/Altai/Mageco and IJK/Stingy/Egoist printer-port joystick interfaces, each with two
  Atari-style joystick sockets. A host gamepad can drive either socket, and optional keyboard
  joystick controls use W/A/S/D plus Space. The interface and socket selections are available
  both in the Avalonia sidebar and the Oric configuration dialog.
- Remote control supports keyboard and joystick injection, paced text entry, BASIC readiness and
  source queries, direct TAP loading, and insert/rewind/eject/status control for the virtual tape.
- The 240 &times; 224 active display in text and 240 &times; 200 hires modes, including serial
  ink, paper, character and screen attributes, alternate character sets, inverse and flashing
  characters, and the three text rows below hires graphics.
- Avalonia Desktop and Avalonia Browser integration, with ROM and audio configuration.
- Terminal integration with a 40 &times; 28 glyph-command text display, keyboard input, BASIC
  copy/paste, virtual tape controls, joystick configuration, and ROM download/selection. The
  Terminal host has no audio or hi-res pixel output.

## Not yet implemented

- Cassette pulse input/output, tape recording/`CSAVE`, and software that bypasses the Atmos ROM
  with a custom pulse loader. Because the current transport is a logical byte stream rather than a
  timed cassette signal, physical Play/Stop and continuous fast-forward/reverse controls do not
  apply.
- Microdisc or Jasmin floppy controllers and disk images.
- Printer output, snapshots and Oric-specific monitor commands.
- Cycle-level ULA bus contention and analogue AY output filtering.
- The Oric-1, Oric-1 16K, Pravetz and Telestrat variants.

## ROMs

The Atmos needs one copyrighted 16 KB firmware image. See [ROMs](roms.md) for the accepted image,
checksum, storage locations and the download acknowledgement.
