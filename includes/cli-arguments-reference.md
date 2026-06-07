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
| `--runLoadedProgram` | Run the loaded program after loading. Requires `--start` and a host-supported load source. In the common flow that means `--loadPrg`; Avalonia Desktop also extends this to `--loadD64` for C64. |

These are the **common**, system-agnostic automated-startup flags handled by the shared startup
pipeline. Host- or system-specific extensions build on top of them.

### C64 startup parameters *(Avalonia Desktop only)*

These parameters extend the common automated-startup flow above. They are parsed by the Avalonia
Desktop entry point and applied by the C64 Avalonia shell plugin, so they are **not available in
the Headless app today**.

#### C64 runtime config

These flags override the C64 host config before the system starts. They apply for **any** C64
start path — plain `--start`, `--loadPrg`, BASIC paste, or `--loadD64`.

| Argument | Description |
|---|---|
| `--keyboardJoystickEnabled` | Force-enable the C64 keyboard-emulated joystick. Requires `--system C64`. |
| `--keyboardJoystickNumber <1\|2>` | C64 joystick port the keyboard emulates (and which gamepad port drives). Implies `--keyboardJoystickEnabled`. Requires `--system C64`. |
| `--audioEnabled <true\|false>` | Override the C64 audio-enable config before the system starts. Omit to keep the existing value. Requires `--system C64`. |

#### C64 automated startup extensions

These flags add C64-specific startup behavior on top of the common automated-startup parameters.
They still depend on the common flags such as `--system`, `--start`, and `--waitForSystemReady`.

| Argument | Description |
|---|---|
| `--loadD64 <path>` | Local `.d64` path to use at startup. Requires `--system C64`, `--start`, `--waitForSystemReady`, and exactly one of `--d64Program` or `--diskMount`. Mutually exclusive with `--loadPrg`. |
| `--d64Program <name\|*>` | Extract the named PRG from the disk image and direct-load it into memory (no disk mount). `*` selects the first directory entry. Mutually exclusive with `--diskMount`. |
| `--diskMount` | Mount the disk image in drive 8 and prepare to issue `LOAD"*",8,1` + `RUN` via the keyboard buffer. Mutually exclusive with `--d64Program`. |

When used together with `--loadD64`, the common `--runLoadedProgram` flag controls whether the
disk's `RunCommands` are pasted after the load / mount:

- `--diskMount`: `LOAD"*",8,1` + `RUN`
- `--d64Program`: `RUN`

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
