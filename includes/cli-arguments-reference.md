There are two **mutually exclusive** modes:

- **Scripting mode** — a Lua script controls system selection, start, load, and lifecycle.
- **Automated startup mode** — the CLI directly selects a system, optionally loads a `.prg`, and starts.

!!! important
    `--script` / `--scriptDir` are **mutually exclusive** with `--system`, `--systemVariant`, `--start`, `--waitForSystemReady`, `--loadPrg`, and `--runLoadedProgram`. The script is responsible for all emulator setup and lifecycle. Combining them is an error.

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
| `--console-log` / `-c` | *(Avalonia Desktop only — Headless always logs to console.)* Enable console logging output. |
| `--log-level <level>` / `-l <level>` | Set console log level (`Trace` / `Debug` / `Information` / `Warning` / `Error`). |

### Debug adapter

For the user guide on attaching VS Code, see [Tools / VSCode debugger / Debugging](../tools/vscode-debugger/debugging.md).

| Argument | Description |
|---|---|
| `--enableExternalDebug` | Enable VS Code debug adapter (DAP) over TCP. |
| `--debug-port <port>` | TCP port for the debug adapter (default: `6502`). |
| `--debug-bind-address <ip>` | IP address to bind the debug adapter server to (default: `127.0.0.1`). |
| `--debug-wait` | Wait for a debug client to connect before starting. |

### Remote control

For protocol and command list, see [Tools / Remote control](../tools/remote-control/overview.md).

| Argument | Description |
|---|---|
| `--remote-port <port>` | Start the TCP remote control server on this port. |
| `--remote-bind-address <ip>` | IP address to bind the remote control server to (default: `127.0.0.1`). |
| `--allow-remote-quit` | *(Headless only.)* Allow the `emu.quit` remote control command to terminate the app. |
