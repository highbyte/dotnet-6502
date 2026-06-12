# Terminal (TUI) app

![Terminal (TUI) app, C64 Basic](../../assets/screenshots/Terminal_C64_Basic.png){ width="50%" }

Interactive host app that runs the emulator **inside a real terminal**, using
[Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) v2 for the window/control chrome. The
emulated text-mode screen is rendered as colored Unicode cells, so it works over SSH and inside
`tmux`/`screen`. Text mode only — there is no audio and no bitmap/sprite graphics output.

Technologies:

- UI: [`Terminal.Gui`](https://github.com/gui-cs/Terminal.Gui) v2 controls.
- Rendering: [`Highbyte.DotNet6502.Impl.Terminal`](../../libraries/implementation/terminal.md) — a
  system-agnostic render target that consumes the system's video-command stream into terminal cells.
- Input: [`Highbyte.DotNet6502.Impl.Terminal`](../../libraries/implementation/terminal.md).
- Audio: none (terminals have no audio output).

Like the other host apps, the entry exe holds **no compile-time reference to any emulated system**.
Systems arrive at runtime via plugin discovery: engine plugins (`Impl.Terminal.<System>`) register
the system, and shell plugins (`App.Terminal.Shell.<System>`) contribute the per-system menu, info
panel, and config dialog. See [Architecture](../../architecture.md) and
[Systems.Plugins](../../libraries/core/dotnet6502-systems-plugins.md).

## Supported systems

- **C64** (`Impl.Terminal.Commodore64` + `App.Terminal.Shell.Commodore64`) — character mode only.
  PETSCII graphics screen codes are mapped best-effort to Unicode box-drawing / block / shade glyphs
  for the built-in (uppercase/graphics) charset.
- **VIC-20** (`Impl.Terminal.Vic20` + `App.Terminal.Shell.Vic20`) — character mode only.

The screen pane resizes automatically to fit the running system's frame (C64 ≈ 40 columns,
VIC-20 ≈ 22 columns), and the status/logs column reflows around it. The emulated screen is drawn
without an extra framing box — each system renders its own coloured screen border, which is wide and
wasteful in a terminal, so the view crops it down to a thin, consistent border on every side. This
keeps the C64 (29 cells tall including its border) within a default ~30-row terminal without the user
having to enlarge the window. How much border to keep on each side is configurable in
`appsettings.json`: `VerticalBorderRows` (top/bottom, default `1`) and `HorizontalBorderColumns`
(left/right, default `2`); set either to `0` to show the full emulated border on that axis.

## Layout and controls

The window has a **Controls** column (left), the **Screen** in the middle, and a **Stats** box plus
a tabbed **Info / Config / Logs** pane on the right. A per-system menu (contributed by the shell
plugin) appears below the standard controls.

Hotkeys (shown in the bottom hint line):

| Key | Action |
|-----|--------|
| `9` | Cycle the selected **System** (only while stopped) |
| `0` | Cycle the selected **Variant** (only while stopped) |
| `F9` | Start / Stop toggle |
| `F10` | Quit the app |
| `F11` | Toggle the **Stats** (instrumentation) box |
| `F12` | Toggle the **Monitor** |
| `Tab` | Move focus to the emulator screen (so typing reaches the running system) |

Hotkeys are suppressed while a modal dialog (config, file picker, monitor) is open, so keys —
including the digits `9`/`0` — reach the dialog's fields normally. `F1`–`F8` are reserved for the
emulated systems (e.g. the C64 function keys).

## Config dialog

Each system contributes a **Config** dialog (opened from its menu) for editing settings while the
emulator is stopped, with live validation:

- **C64** — ROM directory/files (with auto-download), SwiftLink (enable, cartridge I/O address,
  interrupt mode, receive mode, transport, TCP host/port, connect-on-boot), and keyboard joystick
  (enable + port 1/2). Keyboard-joystick settings can also be toggled live from the C64 menu.
- **VIC-20** — ROM directory/files (with auto-download).

See [Systems / C64 / ROMs](../../systems/c64/roms.md), [Systems / VIC-20 / ROMs](../../systems/vic20/roms.md),
and [Systems / C64 / SwiftLink](../../systems/c64/swiftlink.md).

## Monitor

Press the **Monitor** button or `F12` (while a system is running or paused) to open the built-in
6502 machine-code monitor — the same command set as the other host apps' monitor (type `?` for
help). It opens full-screen over the UI, shows the accumulated output plus the current CPU/system
status, and has a command input line. While the monitor is open, emulation is halted so the
monitor has exclusive access to the CPU and memory; closing it (`Esc` / `F12`) or a `g` (go) command
resumes. Breakpoints and other break triggers open the monitor automatically.

Load/save commands that take no filename (`l`, and the C64 `lb`) open a terminal file picker;
the `ll <file>` / `llb <file>` variants take an explicit path. See
[Monitor library](../../libraries/core/dotnet6502-monitor.md).

## Stats

Press the **Stats** button or `F11` to show host instrumentation (FPS, per-frame timings, …) in the
right-hand box while a system runs.

## How to run locally for development

For development system requirements, see [Development](../../home/development.md). The Terminal app
needs a real TTY, so run it from a terminal (not a debugger output pane).

### From the command line

```sh
dotnet run --project src/apps/Terminal/Highbyte.DotNet6502.App.Terminal
```

A headless self-test that needs no TTY (boots a system, runs a number of frames, and dumps the
rendered screen as text) is useful for CI / smoke testing:

```sh
dotnet run --project src/apps/Terminal/Highbyte.DotNet6502.App.Terminal -- --selftest --frames 400 --system C64
```

### VS Code

Use the **Terminal (TUI) - Launch** configuration (runs in the integrated terminal so a real TTY is
available). **Terminal (TUI) - Launch (Self-test)** runs the headless self-test.

## Limitations

- Text (character) mode only — no bitmap, multicolor, or sprite graphics are rendered.
- No audio.
- Requires a terminal with Unicode and 24-bit ("true color") support for correct glyphs and colors.
- PETSCII graphics are approximated with the nearest common Unicode glyphs, so they will not match
  the C64 exactly.

For general project limitations, see [Limitations](../../home/limitations.md).
