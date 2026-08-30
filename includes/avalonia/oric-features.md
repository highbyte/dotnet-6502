<!--
Shared Oric Atmos feature documentation for the Avalonia apps (Browser + Desktop), included via
pymdownx.snippets into docs/host-apps/avalonia/oric.md.

Sections start at ## (the consumer page supplies the # title). See includes/avalonia/README.md.
-->

## ROM

The Oric Atmos needs its 16 KB BASIC 1.1b system ROM. The configuration dialog can download it
after an ownership/licence acknowledgement. See [Systems / Oric / ROMs](../../systems/oric/roms.md)
for the accepted image, checksum, and legal notice.

=== "Browser"
    Upload the ROM through the Oric configuration dialog, or use the auto-download option. The
    browser stores the loaded ROM in browser storage.

=== "Desktop"
    Select a local ROM, or use the auto-download option. The default directory is
    `~/Documents/Highbyte/DotNet6502/roms/Oric` on macOS/Linux and the corresponding Documents
    directory on Windows.

## Display and audio

The rasterizer displays the 240 × 224 active area in text and hi-res modes, including serial
ink/paper and character attributes, alternate character sets, inverse and flashing characters,
and the three text rows below hi-res graphics. A lightweight text-only command-stream renderer is
also selectable in the configuration dialog.

AY-3-8912 audio provides three tone channels plus noise and envelope generation.

=== "Browser"
    Audio playback uses WebAudio.

=== "Desktop"
    Audio playback uses OpenAL.

## Keyboard and joystick

Host keyboard input supports US English and Swedish layouts, with automatic detection or an
explicit setting. PC <kbd>Alt</kbd> and Mac <kbd>Option</kbd> map to the Atmos **FUNCT** key; PC
<kbd>Backspace</kbd> and Mac <kbd>Delete</kbd> map to **DEL**. The running app's **Information** tab
shows the complete mapping and common Atmos BASIC control combinations.

The PASE/Altai/Mageco and IJK/Stingy/Egoist printer-port joystick interfaces are supported, each
with two Atari-style sockets. A host gamepad can drive either socket. Optional keyboard joystick
controls use <kbd>W</kbd>/<kbd>A</kbd>/<kbd>S</kbd>/<kbd>D</kbd> and <kbd>Space</kbd>; while enabled,
those keys are consumed as joystick input instead of also reaching the Oric keyboard.

=== "Browser"
    Gamepad input uses the browser Gamepad API.

=== "Desktop"
    Gamepad input uses SDL.

## BASIC, tape, and downloadable programs

The sidebar can copy the current tokenized Atmos BASIC program to the clipboard as source text and
paste source back through the ROM keyboard input path. BASIC keywords must be uppercase.

Byte-level `.tap` images can be attached to the virtual tape, loaded directly, rewound, ejected,
or moved to the previous/next parsed file. The tape status shows its byte position and current or
next file. Standard ROM `CLOAD` calls read successive records from the attached image. Cassette
pulses, physical transport timing, recording, and custom pulse loaders are not emulated.

**Download & Run** fetches a curated Oric program as a TAP image, optionally extracting a named
entry from a ZIP archive, caches it, and starts it through the Atmos ROM cassette routine.

=== "Browser"
    Downloads use the browser host's configured CORS proxy.

=== "Desktop"
    Downloads use the source URL directly.

## Oric menu

The expandable Oric sidebar shared by both runtimes exposes:

- **BASIC** — copy and paste source.
- **Download & Run** — select, download, and start a curated TAP program.
- **Tape** — attach/replace, rewind, move between records, inspect status, and eject a `.tap` image.
- **Load / Save** — directly load a TAP file and load bundled text, graphics, and sound examples.
- **Joystick / Configuration** — select the interface and sockets, enable keyboard joystick, and
  configure the ROM, audio, keyboard layout, CPU profile, and render provider.
