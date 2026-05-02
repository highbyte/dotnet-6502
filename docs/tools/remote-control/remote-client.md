# Remote Client console app

`dotnet-6502-remote` is a thin CLI wrapper that hides the JSON framing, handles connection setup, and formats output for human or pipeline consumption.

Pre-built binaries are available for Windows, Linux, and macOS. The remote client connects to a running [Avalonia Desktop](../../desktop-apps/avalonia-desktop.md) or [Headless](../../desktop-apps/headless.md) emulator via its TCP remote control endpoint. See [TCP protocol](tcp-protocol.md) for the protocol, available commands, and full reference.

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

## Install via Package Manager

**Prerequisites:** Install [Homebrew](https://brew.sh/) (macOS/Linux) or [Scoop](https://scoop.sh/) (Windows) if you don't have them already.

### macOS (Homebrew)

```bash
brew tap highbyte/dotnet-6502
brew install --formula dotnet-6502-remote
```

### Linux (Homebrew)

```bash
brew tap highbyte/dotnet-6502
brew install --formula dotnet-6502-remote
```

### Windows (Scoop)

```powershell
scoop bucket add dotnet-6502 https://github.com/highbyte/scoop-dotnet-6502
scoop install dotnet-6502-remote
```

### Launching

After installing, start an emulator with remote control enabled (see [Remote control overview](overview.md)), then from any terminal:

```
dotnet-6502-remote emu.state
dotnet-6502-remote --port 6510 cpu.get
```

Run `dotnet-6502-remote --help` for the full command list.

### Updating

```bash
# macOS / Linux
brew update && brew upgrade dotnet-6502-remote
```

```powershell
# Windows
scoop update
scoop update dotnet-6502-remote
```

### Uninstalling

```bash
# macOS / Linux
brew uninstall dotnet-6502-remote
brew untap highbyte/dotnet-6502
```

```powershell
# Windows
scoop uninstall dotnet-6502-remote
scoop bucket rm dotnet-6502
```

---

## Install via manual download

Download the latest release for your platform from the [Releases](https://github.com/highbyte/dotnet-6502/releases) page under Assets.

| Platform | Download |
|----------|----------|
| Windows x64 | `DotNet6502-RemoteClient-win-x64.zip` |
| Windows ARM64 | `DotNet6502-RemoteClient-win-arm64.zip` |
| Linux x64 | `DotNet6502-RemoteClient-linux-x64.zip` |
| Linux ARM64 | `DotNet6502-RemoteClient-linux-arm64.zip` |
| macOS ARM64 (Apple Silicon) | `DotNet6502-RemoteClient-osx-arm64.zip` |

### Windows

1. Extract the `.zip` file to a folder.
2. Open a terminal in that folder and run:

   ```
   Highbyte.DotNet6502.App.RemoteClient.exe --help
   ```

#### SmartScreen Warning

Since the application is not code-signed, Windows SmartScreen may show a warning the first time you run it:

> "Windows protected your PC - Microsoft Defender SmartScreen prevented an unrecognized app from starting."

**To proceed:**

1. Click **"More info"**
2. Click **"Run anyway"**

---

### Linux

1. Extract the `.zip` file:

   ```sh
   unzip DotNet6502-RemoteClient-linux-x64.zip -d dotnet6502-remote
   cd dotnet6502-remote
   ```

2. Run the app:

   ```sh
   ./Highbyte.DotNet6502.App.RemoteClient --help
   ```

---

### macOS

!!! note
    The macOS build is not notarized with Apple.

1. Extract the `.zip` file.

2. Open Terminal and navigate to the extracted folder:

   ```sh
   cd /path/to/extracted/folder
   ```

3. Remove the quarantine attribute:

   ```sh
   xattr -cr .
   ```

4. Run the app:

   ```sh
   ./Highbyte.DotNet6502.App.RemoteClient --help
   ```

---

### Verifying download integrity (optional)

Each release includes SHA256 checksum files (`checksums-*.sha256`) to verify your download.

#### Windows (PowerShell)

```powershell
(Get-FileHash -Algorithm SHA256 DotNet6502-RemoteClient-win-x64.zip).Hash.ToLower()
```

#### Linux

```sh
sha256sum DotNet6502-RemoteClient-linux-x64.zip
```

#### macOS

```sh
shasum -a 256 DotNet6502-RemoteClient-osx-arm64.zip
```

Compare the output with the corresponding entry in the `checksums-*.sha256` file.
