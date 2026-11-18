# Avalonia app

A cross-platform emulator front-end written with [Avalonia UI](https://avaloniaui.net/), shipped as
**two runtimes that share almost all code, including the UI**:

- **[Browser](browser.md)** — runs entirely in the browser via WebAssembly (no install).
- **[Desktop](desktop.md)** — runs natively on Windows, macOS and Linux.

Because the two runtimes share their UI, the system-specific features are documented once per system,
with the small Browser-vs-Desktop differences called out inline:

- [C64 in the Avalonia apps](c64.md)
- [VIC-20 in the Avalonia apps](vic20.md)
- [Apple II Plus in the Avalonia apps](apple2.md)
- [Oric Atmos in the Avalonia apps](oric.md)
- [Generic computer in the Avalonia apps](generic.md)

Runtime-specific topics:

- [Desktop automation](automation.md) — UI automation and accessibility.
- [Desktop troubleshooting](troubleshooting.md) — platform-specific notes.

## Input recording and replay

Recording/replay controls are shared by Desktop and Browser and are currently available
for C64. See [Input recording and replay](../../tools/recording-replay.md) for the demo
format, shared input behavior, compatibility, and file limits.

1. Start a C64 and load the program to record.
2. Expand **Recording / replay** and click **Record**.
3. Play normally, then click **Stop / save**. Desktop opens a save picker; Browser downloads
   the file. If the picker is cancelled, **Save demo** retries the completed recording.
4. Click **Open demo** to replace the current machine state and start playback.

Use the ordinary **Pause** and **Start** controls to pause/resume. **Step frame** advances
one emulated frame while paused, in either recording or playback. **Stop replay** ends
playback early. **Restart** restores the demo's initial state and starts playback again.

The bottom status bar shows mode, frame progress, completion, and failure. While replay
owns input it explicitly says **live input disabled**, including while paused, alongside
the Desktop TCP Remote indicator. Machine-input and conflicting mutation controls are disabled.
See [Playback and input](../../tools/recording-replay.md#playback-and-input) for the shared
ownership and cleanup rules.

Completed recordings are retained in memory until saved. Desktop and Browser exchange the
same demo files with Headless; see its [playback and validation options](../headless/overview.md#demo-playback-and-validation).
