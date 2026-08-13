# Overview of the Apple II system

A partial implementation of an Apple II Plus.

Core library: `Highbyte.DotNet6502.Systems.Apple2`.

CPU: the NMOS **6502** model (`nmos6502`) by default, as in the Apple II/II+/unenhanced IIe.
The NCR **65C02** model (`ncr65c02`) can be selected in the system configuration.

The currently supported system variant is `APPLE2PLUS`.

## Current capabilities

- Run Applesoft BASIC and the Autostart Monitor from user-supplied Apple II Plus ROMs.
- 48 KB base RAM and an optional 16 KB language card, enabled by default. The resulting 64 KB
  configuration supports ProDOS 8. See [Language card](#language-card).
- 12 KB system ROM. Both 12 KB ROM images and the common 20 KB image layout are accepted.
- Apple II text and graphics:
    - 40 &times; 24 text with normal, inverse, and flashing characters.
    - 40 &times; 48 lo-res graphics.
    - 280 &times; 192 hi-res graphics with NTSC artifact colours.
    - Mixed graphics and text modes.
    - Composite colour, green, white, and amber monitor options.
- Two video render providers:
    - **Rasterizer** (default) renders text, lo-res, and hi-res graphics from the character ROM.
    - **VideoCommands** is a lightweight text-only fallback for character-cell hosts.
- Read-only Disk II controller in slot 6. It boots standard 16-sector images in DOS 3.3 or
  ProDOS sector order, detected automatically. See [Disk II](disk2.md).
- Apple II game-port support for paddles and two buttons. A host gamepad works directly; optional
  keyboard joystick controls use `W`/`A`/`S`/`D`, Space, and Left Shift.
- Host keyboard input with Shift, Control, and key repeat. US English and Swedish layouts are
  supported, with automatic host-layout detection or an explicit setting.
- Built-in speaker audio, including software-generated effects and sampled audio.
- CTRL-RESET using **Ctrl + F12**.
- Program loading in the Avalonia apps:
    - Tokenized Applesoft BASIC files and DOS 3.3 binary files.
    - Applesoft and binary programs loaded directly from DOS 3.3 disk images.
    - Bundled BASIC and assembly examples.
    - Curated downloadable programs and bootable games.
- Copy and paste of Applesoft BASIC source in the Avalonia sidebar.
- Remote-control commands for keyboard input, BASIC source, and Disk II operations. See the
  [TCP protocol](../../tools/remote-control/tcp-protocol.md).
- Avalonia Desktop and Avalonia Browser (WASM) apps with configuration for ROMs, display,
  audio, input, and CPU compatibility, plus text-mode support in the Terminal (TUI) app.
- Emulator state snapshots, including the language card and inserted disk. See
  [Snapshots](#snapshots).

## Disk II support

The Disk II controller can insert and boot standard 16-sector `.dsk` and `.do` images. DOS 3.3
and ProDOS sector ordering are both supported and detected from the image contents. ProDOS 8 runs
when the language card is enabled.

The Avalonia apps can also use a DOS 3.3 disk image as a file source, loading an Applesoft or
binary program directly into RAM without booting the disk.

The drive is read-only and supports one drive. Disk writing and nibble/flux image formats are not
implemented. See [Disk II](disk2.md) for details.

## Language card

The optional language card adds 16 KB of bank-switched RAM over the system ROM address space,
bringing the machine to 64 KB. It is enabled by default and can be disabled in the configuration
dialog to emulate a plain 48 KB Apple II Plus.

The card allows ProDOS 8 to run and is also used by DOS 3.3 software. Its memory and switch state
are included in emulator snapshots.

## Snapshots

Apple II `.d6502snap` snapshots save the CPU, base RAM, language card, display state, keyboard,
speaker, game port, Disk II controller, and inserted disk. Audio, keyboard-joystick, and monitor
settings can be included as portable settings.

ROMs are not stored in a snapshot; they are loaded from the destination system's configuration
when the snapshot is restored.

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

- Fully NTSC-accurate hi-res artifact colour.
- Disk II writing, a second drive, and nibble/flux disk images.
- Sound cards such as the Mockingboard.
- Peripheral slots other than slot 6, cassette, and the second paddle pair.
- Other Apple II family models, including the original Integer BASIC Apple II and Apple IIgs.

## ROMs

See [ROMs](roms.md).
