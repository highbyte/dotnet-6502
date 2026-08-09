<!--
Shared Apple II Plus feature documentation for the Avalonia apps (Browser + Desktop), included via
pymdownx.snippets into docs/host-apps/avalonia/apple2.md.

Sections start at ## (the consumer page supplies the # title). See includes/avalonia/README.md.
-->

## ROMs

The Apple II Plus needs the system and character generator ROMs. The Disk II boot ROM is optional
and enables disk booting. The configuration dialog can download them after a license
acknowledgement. See [Systems / Apple II / ROMs](../../systems/apple2/roms.md) for accepted images
and other details.

=== "Browser"
    Upload the ROM binaries through the Apple II configuration dialog, or use the auto-download
    option. The browser stores loaded ROMs in local storage.

=== "Desktop"
    Point the app at a directory containing the ROM files, or use the auto-download option. The
    default directory is `~/Documents/Highbyte/DotNet6502/roms/Apple2` on macOS/Linux and the
    corresponding Documents directory on Windows.

## Display

The default rasterizer renders 40-column text, lo-res graphics, hi-res graphics with NTSC artifact
colors, and mixed graphics/text modes. The configuration dialog provides composite color, green,
white, and amber monitor options. A lightweight text-only command-stream renderer is also
available.

## Input and audio

Host keyboard input supports US English and Swedish layouts, with automatic detection or an
explicit setting. A host gamepad drives the Apple II game port. Optional keyboard-joystick controls
use <kbd>W</kbd>/<kbd>A</kbd>/<kbd>S</kbd>/<kbd>D</kbd>, <kbd>Space</kbd>, and <kbd>Left Shift</kbd>.

The built-in speaker supports software-generated effects and sampled audio.

=== "Browser"
    Gamepad input uses the browser Gamepad API, and speaker playback uses WebAudio.

=== "Desktop"
    Gamepad input uses SDL, and speaker playback uses `OpenAL`.

## Memory and Disk II

The optional language card expands the machine from 48 KB to 64 KB and is enabled by default,
allowing ProDOS 8 software to run. It can be disabled in the configuration dialog for a stock
48 KB Apple II Plus.

The read-only Disk II controller supports one drive with DOS 3.3- and ProDOS-ordered `.dsk`/`.do`
images. The sidebar can insert, eject, and boot disks. See
[Systems / Apple II / Disk II](../../systems/apple2/disk2.md) for the supported workflow and
limitations.

## Apple II menu

The Apple II sidebar shared by both runtimes exposes:

- **Copy / Paste** — copy Applesoft BASIC source to the clipboard or paste source into the running
  machine.
- **Download & Run programs** — download and start a curated program; disk-booting titles require
  the Disk II ROM.
- **Disk drive** — insert or eject a disk in drive 1 and boot it with `PR#6`.
- **Load / Save** — load and save Applesoft BASIC files, load and start binaries, extract and run
  RAM-resident files from DOS 3.3 disk images, and load bundled examples.
- **Configuration** — manage ROMs, the language card, monitor color, renderer, speaker audio,
  keyboard layout, keyboard joystick, and CPU compatibility.
