# Remote Client console app

`dotnet-6502-remote` is a thin CLI wrapper that hides the JSON framing, handles connection setup, and formats output for human or pipeline consumption.

Pre-built binaries are available for Windows, Linux, and macOS. The remote client connects to a running [Avalonia Desktop](../../host-apps/avalonia/desktop.md) or [Headless](../../host-apps/headless/overview.md) emulator via its TCP remote control endpoint. See [TCP protocol](tcp-protocol.md) for the protocol, available commands, and full reference.

## Global options

| Option          | Default     | Description                  |
|-----------------|-------------|------------------------------|
| `--host <host>` | `127.0.0.1` | Server hostname or IP        |
| `--port <port>` | `6510`      | TCP port                     |
| `--help`        |             | Print usage and exit         |

Exit codes: `0` = success, `1` = server returned an error or connection failed, `2` = bad arguments.

## Usage examples

```sh
# Check emulator state
dotnet-6502-remote emu.state

# Start the emulator (also resumes from paused)
dotnet-6502-remote --port 6510 emu.start

# List available systems and variants
dotnet-6502-remote emu.systems
dotnet-6502-remote emu.variants

# Switch system (requires emulator to be stopped first)
dotnet-6502-remote emu.stop
dotnet-6502-remote emu.selectsystem --name C64
dotnet-6502-remote emu.selectvariant --name C64NTSC
dotnet-6502-remote emu.start

# Read 16 bytes from $C000
dotnet-6502-remote mem.read --addr C000 --len 16

# Write bytes to $C000
dotnet-6502-remote mem.write --addr C000 --data 169,42,133,254

# Get CPU registers
dotnet-6502-remote cpu.get

# Set CPU registers (set A and force InterruptDisable flag)
dotnet-6502-remote cpu.set --a 42 --flags "----I---"

# Jump to a specific address
dotnet-6502-remote cpu.set --pc C000

# Load a PRG file into C64 memory
dotnet-6502-remote c64.loadprg --file /path/to/program.prg

# Set joystick port 1: up + fire
dotnet-6502-remote joystick.set --port 1 --up --fire

# Hold joystick port 1 up + fire until release
dotnet-6502-remote joystick.press --port 1 --up --fire

# Release held joystick up on port 1
dotnet-6502-remote joystick.release --port 1 --up

# Release all held joystick actions on port 1
dotnet-6502-remote joystick.releaseall --port 1

# Clear joystick port 1 up + fire explicitly for the next frame only
dotnet-6502-remote joystick.set --port 1 --no-up --fire false

# Press and release the Return key
dotnet-6502-remote keyboard.press --key return
dotnet-6502-remote keyboard.release --key return

# Paste text into the C64 keyboard buffer (C64 only; use lowercase — see c64.type section in TCP protocol)
dotnet-6502-remote c64.type --text "load\"*\",8,1"

# Save / restore a full emulator-state snapshot (path is on the emulator host)
dotnet-6502-remote emu.savesnapshot --path /tmp/state.d6502snap
dotnet-6502-remote emu.loadsnapshot --path /tmp/state.d6502snap

# Step the (paused) emulator a fixed number of frames, then screenshot the result
dotnet-6502-remote emu.runframes --count 1

# Take a screenshot and save to a file
dotnet-6502-remote screenshot --output /tmp/screen.png

# Display a message in the Log tab
dotnet-6502-remote ui.message --text "Checkpoint reached" --level info

# Quit the headless emulator (requires --allow-remote-quit on server)
dotnet-6502-remote emu.quit
```

For end-to-end automation patterns, see [Examples](examples.md).

## Run from source (development)

```sh
# Run directly from source
dotnet run --project src/apps/Highbyte.DotNet6502.App.RemoteClient -- --help

# Or build and add to PATH
dotnet build src/apps/Highbyte.DotNet6502.App.RemoteClient -c Release
# binary: src/apps/Highbyte.DotNet6502.App.RemoteClient/bin/Release/net10.0/Highbyte.DotNet6502.App.RemoteClient
```

---

## Installation

Pre-built binaries (`dotnet-6502-remote`) are available via Homebrew (macOS/Linux), Scoop (Windows), or manual download. See [Desktop apps installation](../../host-apps/installation.md#install-via-package-manager) for the install / update / remove commands and manual-download details.

After installing, start an emulator with remote control enabled (see [Remote control overview](overview.md)), then run the client from any terminal:

```sh
dotnet-6502-remote emu.state
dotnet-6502-remote --port 6510 cpu.get
```

Run `dotnet-6502-remote --help` for the full command list.
