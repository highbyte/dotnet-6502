### General parameters *(system-agnostic)*

These are interpreted by the shared startup pipeline and are valid for any system.

#### Updates

Handled before the emulator/UI starts; each prints its result and exits immediately. Update checks
require a package-manager install (Homebrew / Scoop) — manual-download and development builds report
*not managed*. For how detection works and the on/off setting, see
[Staying up to date](../../host-apps/installation.md#staying-up-to-date).

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--version` | Print the app version and exit. | — | `--version` |
| `--check-update` | Check for a newer release and print the result. Ignores the `UpdateCheckEnabled` setting and the CI / `DOTNET6502_NO_UPDATE_CHECK` suppression. | — | `--check-update` |
| `--update` | Check and, if an update is available on a package-manager install, run the `brew`/`scoop` upgrade in the foreground, then exit. | — | `--update` |

Host-specific notes: on **Avalonia Desktop** these flags run before the GUI window opens and, on
Windows, attach to the invoking console so their output is visible. On **Headless** the output goes to
stdout. The **Remote Client** exposes the same flags (see its [Global options](../../tools/remote-control/remote-client.md#global-options)) but never checks automatically.

#### Scripting

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--script <path>` | Load and run a Lua script. Can be specified multiple times. | Exclusive with all automated-startup parameters. | `--script scripts/demo.lua` |
| `--scriptDir <path>` | Override the effective script directory. When omitted, desktop hosts use the configured directory or `~/Documents/Highbyte/DotNet6502/scripts`. | Exclusive with all automated-startup parameters. | `--scriptDir ./scripts` |

#### System selection & lifecycle

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--system <name>` | Select a system (e.g. `C64`, `Generic`). | — | `--system C64` |
| `--systemVariant <name>` | Select a system variant (e.g. `C64PAL`, `C64NTSC`). | Requires `--system`. | `--systemVariant C64PAL` |
| `--start` | Auto-start the emulator after selection. | Requires `--system`. | `--start` |
| `--waitForSystemReady` | Wait until the system reports ready (e.g. C64 BASIC prompt) before continuing. | Requires `--start`. | `--waitForSystemReady` |
| `--loadPrg <path>` | Load a local `.prg` file into memory. | Requires `--start`. Exclusive with `--loadPrgUrl` and C64 `--loadD64` / `--loadD64Url` / `--loadCrt` / `--loadCrtUrl`. | `--loadPrg game.prg` |
| `--loadPrgUrl <url>` | *(Avalonia Desktop only.)* Fetch a `.prg` over HTTP(S) and load it into memory. | Requires `--start`. Exclusive with `--loadPrg` and C64 `--loadD64` / `--loadD64Url` / `--loadCrt` / `--loadCrtUrl`. | `--loadPrgUrl https://example.com/game.prg` |
| `--runLoadedProgram` | Run the loaded program after loading. | Requires `--start` and a load source (`--loadPrg` / `--loadPrgUrl`, or C64 `--loadD64` / `--loadD64Url`). | `--runLoadedProgram` |
| `--load-snapshot <path>` | Restore a full `.d6502snap` emulator-state snapshot. Relative paths are resolved from the shared snapshot directory. The snapshot's manifest determines the system, so no `--system` is needed. The machine is left paused after restore; add `--start` to resume. | Exclusive with `--system`, `--systemVariant`, `--loadPrg` / `--loadPrgUrl`, `--runLoadedProgram`, and `--script`. `--waitForSystemReady` requires `--start`. | `--load-snapshot state.d6502snap` |

`--loadPrg` / `--loadPrgUrl` copy the file's bytes to the load address in its 2-byte header — but
how a loaded `.prg` is *interpreted and run* is system-specific. The systems documented below may
add behavior on top of this (e.g. based on the load address); see the relevant system's parameter
group. Cartridge images use the C64-specific `.crt` startup flow instead of PRG loading.

#### Logging

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--console-log` / `-c` | *(Avalonia Desktop only — Headless always logs to console.)* Enable console logging output. | — | `--console-log` |
| `--log-level <level>` / `-l <level>` | Console log level (`Trace` / `Debug` / `Information` / `Warning` / `Error` / `Critical`). | — | `--log-level Debug` |

#### Diagnostics & auto-exit

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--show-storage-paths` | Print effective storage paths and exit without starting the app. Also supported by Headless. | — | `--show-storage-paths` |
| `--stats-interval <seconds>` | *(Avalonia Desktop only.)* Log an instrumentation snapshot every N seconds after startup completes. | Requires `--start`. | `--stats-interval 5` |
| `--exit-after <seconds>` | *(Avalonia Desktop only.)* Quit the app N seconds after startup completes. | Requires `--start`. | `--exit-after 60` |

#### Debug adapter

For the user guide on attaching VS Code, see [Tools / VSCode debugger / Debugging](../../tools/vscode-debugger/debugging.md).

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--enableExternalDebug` | Start the VS Code debug adapter (DAP) over TCP. This is the flag that enables the server. | — | `--enableExternalDebug` |
| `--debug-port <port>` | TCP port for the debug adapter (default `6502`). | Requires `--enableExternalDebug`. | `--debug-port 6502` |
| `--debug-bind-address <ip>` | IP address to bind the debug adapter to (default `127.0.0.1`). | Requires `--enableExternalDebug`. | `--debug-bind-address 0.0.0.0` |
| `--debug-wait` | Wait for a debug client to connect before starting. | Requires `--enableExternalDebug`. | `--debug-wait` |

#### Remote control

For protocol and command list, see [Tools / Remote control](../../tools/remote-control/overview.md).

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--remote-port <port>` | Start the TCP remote control server on this port. | — | `--remote-port 6510` |
| `--remote-bind-address <ip>` | IP address to bind the remote control server to (default `127.0.0.1`). | Requires `--remote-port`. | `--remote-bind-address 0.0.0.0` |
| `--allow-remote-quit` | *(Headless only.)* Allow the `emu.quit` remote control command to terminate the app. | Requires `--remote-port`. | `--allow-remote-quit` |
