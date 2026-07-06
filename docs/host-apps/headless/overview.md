# Headless app

Cross-platform console app that runs the emulator without any UI, rendering, audio, or user input. Driven entirely by CLI arguments and Lua scripts — useful for automation, scripting, and CI workflows.

Technologies:

- UI: none.
- Rendering: none (null renderer).
- Input: none (null input handler).
- Audio: none (null audio handler).
- Scripting: Lua via [MoonSharp](https://www.moonsharp.org/).

## Installation

See [Desktop apps installation](../installation.md#install-via-package-manager) for package manager and manual download instructions.

## Features

### Systems

- **C64** — requires ROM files (Kernal, Basic, Chargen). See [Systems / C64 / ROMs](../../systems/c64/roms.md).
- **Generic computer** — built-in example 6502 programs (Snake, Scroll, HelloWorld).

### Lua scripting

The same Lua scripting API available in the Avalonia apps is fully supported here. Scripts are the primary way to control the emulator. Example scripts are included in the `scripts/` directory:

| Script | Description |
|---|---|
| `example_emulator_control.lua` | Query state, pause/resume/reset, state-change event hooks. |
| `example_c64_basic_readwrite.lua` | Write a BASIC program via keyboard buffer and read it back. |
| `example_c64_border_cycle.lua` | Cycle C64 border color via memory writes. |
| `example_c64_download_and_run_prg.lua` | Download and run a `.prg` file over HTTP. |
| `example_c64_load_d64.lua` | Load a `.d64` disk image. |
| `example_c64_screenshot.lua` | Save a screenshot to disk. |
| `example_frameadvance.lua` | Advance frames and read frame count. |
| `example_input_kb.lua` | Inject keyboard input. |
| `example_input_joystick.lua` | Inject joystick input. |
| `example_monitor.lua` | Interact with the machine code monitor. |
| `example_file_io.lua` | Read and write files from Lua. |
| `example_http.lua` | Make HTTP requests from Lua. |
| `example_tcp_client.lua` | Connect to a TCP server from Lua. |
| `example_store.lua` | Persist data between script runs using the key-value store. |
| `example_quit.lua` | Quit the emulator from a script. |

For the full Lua API reference, see [Tools / Scripting / Lua API](../../tools/scripting/lua-api.md).

### External debug adapter

The headless app supports the [Debug Adapter Protocol (DAP)](https://microsoft.github.io/debug-adapter-protocol/) over TCP, allowing VS Code to attach a debugger while the emulator runs headless. See [Tools / VSCode debugger / Debugging](../../tools/vscode-debugger/debugging.md) for the user guide.

### Remote control

The headless app can expose a TCP remote control endpoint that lets external processes drive the running emulator. See [Tools / Remote control](../../tools/remote-control/overview.md).
The remote protocol includes `emu.savesnapshot` and `emu.loadsnapshot`. Absolute paths are used as-is on the machine running the headless emulator; relative paths are resolved from the shared snapshot directory.

## CLI arguments

The headless app is driven entirely from the command line.

--8<-- "startup-params/cli-intro.md"

--8<-- "startup-params/cli-general.md"

--8<-- "startup-params/cli-c64.md"

!!! note
    Several automated-startup parameters are currently wired **only in the Avalonia Desktop app** and
    are ignored by the headless app (each is marked *(Avalonia Desktop only)* in the table above):

    - **URL load sources**: `--loadPrgUrl`, `--loadD64Url`, and `--loadCrtUrl` (the headless app loads only a local
      `--loadPrg`).
    - **C64 BASIC paste**: `--basicText` / `--basicFile` / `--basicUrl` / `--runBasic`.
    - **C64 `.d64` startup**: `--loadD64` / `--loadD64Url` / `--loadD64ZipEntry` / `--d64Program` / `--diskMount`.
    - **C64 `.crt` startup**: `--loadCrt` / `--loadCrtUrl` / `--loadCrtZipEntry`.
    - **C64 runtime config**: `--keyboardJoystickEnabled` / `--keyboardJoystickNumber` / `--audioEnabled`.
    - **Diagnostics & auto-exit**: `--stats-interval` / `--exit-after`.

    The C64-specific flows need a C64 shell plugin (to apply C64-runtime config and run the
    disk-mount / direct-load / BASIC-paste flow), which the headless app does not host. The shared
    helpers are host-agnostic, so adding equivalent flags to the headless app is possible but is out
    of scope for the initial release.

### Examples

Examples below use `dotnet-6502-headless` as installed via Homebrew or Scoop.

- Manual download (self-contained binary): replace with `./Highbyte.DotNet6502.App.Headless` (or `Highbyte.DotNet6502.App.Headless.exe` on Windows).
- Running from source: replace with `dotnet run --project src/apps/Highbyte.DotNet6502.App.Headless --`.

Start a C64 and run a Lua script (script owns all setup and lifecycle):

```sh
dotnet-6502-headless --script scripts/example_c64_basic_readwrite.lua
```

Start with debug adapter listening on `127.0.0.1:6502`, waiting for client (no script):

```sh
dotnet-6502-headless --system C64 --start --enableExternalDebug --debug-port 6502 --debug-wait
```

Start with debug adapter listening on all interfaces at port 6502 (trusted networks only):

```sh
dotnet-6502-headless --system C64 --start --enableExternalDebug --debug-port 6502 --debug-bind-address 0.0.0.0 --debug-wait
```

Start with remote control server on port 6510 (loopback only):

```sh
dotnet-6502-headless --system C64 --start --remote-port 6510
```

Start with remote control server accessible from the network (trusted networks only):

```sh
dotnet-6502-headless --system C64 --start --remote-port 6510 --remote-bind-address 0.0.0.0
```

Start with remote control and allow `emu.quit` command:

```sh
dotnet-6502-headless --system C64 --start --remote-port 6510 --allow-remote-quit
```

Restore an emulator-state snapshot and resume running it (the snapshot's manifest determines the system — no `--system` needed):

```sh
dotnet-6502-headless --load-snapshot state.d6502snap --start
```

Restore a snapshot but leave it paused, with remote control so an external process can step/screenshot it:

```sh
dotnet-6502-headless --load-snapshot state.d6502snap --remote-port 6510
```

Example console output:

```
09:12:01 info: Program[0] Starting headless emulator.
09:12:01 info: Program[0] Creating configuration object.
09:12:01 info: Program[0] Initializing logging.
09:12:01 info: Program[0] Reading emulator config.
09:12:01 info: HeadlessHostApp[0] Creating headless host app.
09:12:01 info: HeadlessHostApp[0] Headless host app initialized.
09:12:01 info: HeadlessHostApp[0] Headless emulator running. Press Ctrl+C to exit.
09:12:01 info: Script[0] Waiting for C64 emulator to start...
09:12:02 info: Script[0] C64 emulator started. Waiting for BASIC to initialize...
09:12:03 info: Script[0] BASIC ready. Typing BASIC program...
09:12:05 info: Script[0] Retrieved BASIC source:
09:12:05 info: Script[0] 10 PRINT "HELLO FROM LUA"
                          20 GOTO 10
09:12:05 info: Script[0] Round-trip check PASSED: both lines found in retrieved source.
```

## Configuration

The shipped `appsettings.json` contains packaged defaults. User overrides are read from the Headless host's `appsettings.user.json` under the OS local application data folder:

- macOS/Linux: `~/.local/share/Highbyte/DotNet6502/Headless/appsettings.user.json`
- Windows: `%LOCALAPPDATA%\Highbyte\DotNet6502\Headless\appsettings.user.json`

When the ROM and script directory settings are empty, Headless uses the shared user content folders under `~/Documents/Highbyte/DotNet6502` (or the Windows Documents equivalent):

- ROMs: `roms/[SYSTEM]`
- Lua scripts: `scripts`
- Snapshots: `snapshots`

Headless does not currently have a `SnapshotDirectory` setting. Startup `--load-snapshot` and TCP remote-control `emu.savesnapshot` / `emu.loadsnapshot` use absolute paths as-is and resolve relative paths from the shared snapshots folder above.

Run `dotnet-6502-headless --show-storage-paths` to print the effective user content, scripts, snapshots, settings, cache, and per-system ROM directories without starting the emulator.

Example overlay:

```json
{
  "Highbyte.DotNet6502.Scripting": {
    "Enabled": true,
    "ScriptDirectory": ""
  },
  "Highbyte.DotNet6502.C64.Headless": {
    "SystemConfig": {
      "ROMDirectory": ""
    }
  }
}
```

## How to run locally for development

For development system requirements, see [Development](../../home/development.md).

### Visual Studio (Windows)

Open solution `dotnet-6502.slnx`. Set project `Highbyte.DotNet6502.App.Headless` as startup and start with F5.

### VSCode

A launch configuration is included. Open the repo in VSCode and use the `Headless` launch profile.
