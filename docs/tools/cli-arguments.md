# CLI arguments

Common command-line arguments shared by the [Avalonia Desktop app](../desktop-apps/avalonia-desktop.md) and the [Headless app](../desktop-apps/headless.md). Both apps accept the same arguments to control logging, scripting, debug adapter, and remote control.

There are two **mutually exclusive** modes for driving the emulator from the command line:

- **Scripting mode** — a Lua script controls system selection, start, load, and lifecycle.
- **Automated startup mode** — the CLI directly selects a system, optionally loads a `.prg`, and starts.

!!! important
    `--script` / `--scriptDir` are **mutually exclusive** with `--system`, `--systemVariant`, `--start`, `--waitForSystemReady`, `--loadPrg`, and `--runLoadedProgram`. The script is responsible for all emulator setup and lifecycle. Combining them is an error.

## Parameter reference

### Scripting parameters

| Argument | Description |
|---|---|
| `--script <path>` | Load and run a Lua script (can be specified multiple times). |
| `--scriptDir <path>` | Override the script directory from `appsettings.json`. |

### Automated startup parameters

| Argument | Description |
|---|---|
| `--system <name>` | Select a system (e.g. `C64`, `Generic`). |
| `--systemVariant <name>` | Select a system variant. Requires `--system`. |
| `--start` | Auto-start the emulator after selection. |
| `--waitForSystemReady` | Wait until the system reports ready before continuing. Requires `--start`. |
| `--loadPrg <path>` | Load a `.prg` file into memory. Requires `--start`. |
| `--runLoadedProgram` | Run the loaded `.prg` file after loading. Requires `--start` and `--loadPrg`. |

### Logging

| Argument | Description |
|---|---|
| `--console-log` / `-c` | Enable console logging *(Avalonia Desktop only — Headless always logs to console)*. |
| `--log-level <level>` / `-l <level>` | Set console log level (`Trace` / `Debug` / `Information` / `Warning` / `Error`). |

### Debug adapter

For the user guide on attaching VS Code, see [Tools / VSCode debugger / Debugging](vscode-debugger/debugging.md).

| Argument | Description |
|---|---|
| `--enableExternalDebug` | Enable VS Code debug adapter (DAP) over TCP. |
| `--debug-port <port>` | TCP port for the debug adapter (default: `6502`). |
| `--debug-bind-address <ip>` | IP address to bind the debug adapter server to (default: `127.0.0.1`). |
| `--debug-wait` | Wait for a debug client to connect before starting. |

### Remote control

For protocol and command list, see [Tools / Remote control](remote-control/overview.md).

| Argument | Description |
|---|---|
| `--remote-port <port>` | Start the TCP remote control server on this port. |
| `--remote-bind-address <ip>` | IP address to bind the remote control server to (default: `127.0.0.1`). |
| `--allow-remote-quit` | Allow the `emu.quit` remote control command to terminate the app *(Headless only)*. |

## Examples

### Avalonia Desktop

```sh
# Run a Lua script (script owns all setup and lifecycle)
./Highbyte.DotNet6502.App.Avalonia.Desktop --script scripts/example_c64_basic_readwrite.lua

# Start C64 and load a .prg file via CLI (no script)
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --loadPrg game.prg --runLoadedProgram

# Start with debug adapter for VS Code, waiting for client
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --enableExternalDebug --debug-port 6502 --debug-wait

# Start with debug adapter bound to all interfaces (use only on trusted networks)
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --enableExternalDebug --debug-port 6502 --debug-bind-address 0.0.0.0

# Start with remote control server on port 6510 (loopback only)
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --remote-port 6510

# Start with remote control server accessible from the network (trusted networks only)
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --remote-port 6510 --remote-bind-address 0.0.0.0
```

### Headless

```sh
# Run a Lua script (script owns all setup and lifecycle)
dotnet-6502-headless --script scripts/example_c64_basic_readwrite.lua

# Start with debug adapter listening on 127.0.0.1:6502, waiting for client (no script)
dotnet-6502-headless --system C64 --start --enableExternalDebug --debug-port 6502 --debug-wait

# Start with debug adapter listening on all interfaces (trusted networks only)
dotnet-6502-headless --system C64 --start --enableExternalDebug --debug-port 6502 --debug-bind-address 0.0.0.0 --debug-wait

# Start with remote control server on port 6510 (loopback only)
dotnet-6502-headless --system C64 --start --remote-port 6510

# Start with remote control accessible from the network (trusted networks only)
dotnet-6502-headless --system C64 --start --remote-port 6510 --remote-bind-address 0.0.0.0

# Start with remote control and allow emu.quit
dotnet-6502-headless --system C64 --start --remote-port 6510 --allow-remote-quit
```
