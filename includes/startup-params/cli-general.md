### General parameters *(system-agnostic)*

These are interpreted by the shared startup pipeline and are valid for any system.

#### Scripting

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--script <path>` | Load and run a Lua script. Can be specified multiple times. | Exclusive with all automated-startup parameters. | `--script scripts/demo.lua` |
| `--scriptDir <path>` | Override the script directory from `appsettings.json`. | Exclusive with all automated-startup parameters. | `--scriptDir ./scripts` |

#### System selection & lifecycle

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--system <name>` | Select a system (e.g. `C64`, `Generic`). | — | `--system C64` |
| `--systemVariant <name>` | Select a system variant (e.g. `C64PAL`, `C64NTSC`). | Requires `--system`. | `--systemVariant C64PAL` |
| `--start` | Auto-start the emulator after selection. | Requires `--system`. | `--start` |
| `--waitForSystemReady` | Wait until the system reports ready (e.g. C64 BASIC prompt) before continuing. | Requires `--start`. | `--waitForSystemReady` |
| `--loadPrg <path>` | Load a local `.prg` file into memory. | Requires `--start`. Exclusive with `--loadPrgUrl` and C64 `--loadD64` / `--loadD64Url`. | `--loadPrg game.prg` |
| `--loadPrgUrl <url>` | *(Avalonia Desktop only.)* Fetch a `.prg` over HTTP(S) and load it into memory. | Requires `--start`. Exclusive with `--loadPrg` and C64 `--loadD64` / `--loadD64Url`. | `--loadPrgUrl https://example.com/game.prg` |
| `--runLoadedProgram` | Run the loaded program after loading. | Requires `--start` and a load source (`--loadPrg` / `--loadPrgUrl`, or C64 `--loadD64` / `--loadD64Url`). | `--runLoadedProgram` |

`--loadPrg` / `--loadPrgUrl` copy the file's bytes to the load address in its 2-byte header — but
how a loaded `.prg` is *interpreted and run* is system-specific. The systems documented below may
add behavior on top of this (e.g. based on the load address); see the relevant system's parameter
group.

#### Logging

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--console-log` / `-c` | *(Avalonia Desktop only — Headless always logs to console.)* Enable console logging output. | — | `--console-log` |
| `--log-level <level>` / `-l <level>` | Console log level (`Trace` / `Debug` / `Information` / `Warning` / `Error` / `Critical`). | — | `--log-level Debug` |

#### Diagnostics & auto-exit *(Avalonia Desktop only)*

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--stats-interval <seconds>` | Log an instrumentation snapshot every N seconds after startup completes. | Requires `--start`. | `--stats-interval 5` |
| `--exit-after <seconds>` | Quit the app N seconds after startup completes. | Requires `--start`. | `--exit-after 60` |

#### Debug adapter

For the user guide on attaching VS Code, see [Tools / VSCode debugger / Debugging](../tools/vscode-debugger/debugging.md).

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--enableExternalDebug` | Start the VS Code debug adapter (DAP) over TCP. This is the flag that enables the server. | — | `--enableExternalDebug` |
| `--debug-port <port>` | TCP port for the debug adapter (default `6502`). | Requires `--enableExternalDebug`. | `--debug-port 6502` |
| `--debug-bind-address <ip>` | IP address to bind the debug adapter to (default `127.0.0.1`). | Requires `--enableExternalDebug`. | `--debug-bind-address 0.0.0.0` |
| `--debug-wait` | Wait for a debug client to connect before starting. | Requires `--enableExternalDebug`. | `--debug-wait` |

#### Remote control

For protocol and command list, see [Tools / Remote control](../tools/remote-control/overview.md).

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--remote-port <port>` | Start the TCP remote control server on this port. | — | `--remote-port 6510` |
| `--remote-bind-address <ip>` | IP address to bind the remote control server to (default `127.0.0.1`). | Requires `--remote-port`. | `--remote-bind-address 0.0.0.0` |
| `--allow-remote-quit` | *(Headless only.)* Allow the `emu.quit` remote control command to terminate the app. | Requires `--remote-port`. | `--allow-remote-quit` |
