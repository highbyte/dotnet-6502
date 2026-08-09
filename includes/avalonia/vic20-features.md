<!--
Shared VIC-20 feature documentation for the Avalonia apps (Browser + Desktop), included via
pymdownx.snippets into docs/host-apps/avalonia/vic20.md.

Sections start at ## (the consumer page supplies the # title). See includes/avalonia/README.md.
-->

## ROMs

The VIC-20 needs the Kernal, BASIC, and character generator ROMs. The configuration dialog can
download them after a license acknowledgement. See [Systems / VIC-20 / ROMs](../../systems/vic20/roms.md)
for file names and other details.

=== "Browser"
    Upload the ROM binaries through the VIC-20 configuration dialog, or use the auto-download
    option. The browser stores loaded ROMs in local storage.

=== "Desktop"
    Point the app at a directory containing the ROM files, or use the auto-download option. The
    default directory is `~/Documents/Highbyte/DotNet6502/roms/VIC20` on macOS/Linux and the
    corresponding Documents directory on Windows.

## Display and input

The Avalonia apps provide the full VIC-I rasterizer display path, including the 22-column NTSC
screen, borders, color RAM, and custom character data. A lightweight command-stream renderer is
also selectable in the configuration dialog.

Keyboard input uses the shared host-agnostic VIC-20 keyboard matrix through Avalonia. The
**Information** tab in the running app shows the host-key mapping.

Audio, joystick input, PAL mode, and RAM expansions are not currently implemented. See the
[VIC-20 system overview](../../systems/vic20/overview.md#not-yet-implemented) for the full list.

## VIC-20 menu

The VIC-20 sidebar shared by both runtimes exposes:

- **Copy / Paste** — copy the current BASIC listing to the clipboard or paste text into the
  running machine.
- **Load / Save** — load and save BASIC `.prg` files, load and start a binary, and load bundled
  assembly or BASIC examples.
- **Configuration** — choose the CPU compatibility profile and render provider, and manage the
  required ROMs.
