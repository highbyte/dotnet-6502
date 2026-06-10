There are two **mutually exclusive** ways to drive startup from the command line:

- **Scripting mode** — a Lua script selects the system and controls start, load, and lifecycle.
- **Automated startup mode** — CLI flags select a system, optionally load a program, and start it.

Parameters fall into two categories, called out in the section headings below:

- **General (system-agnostic)** — interpreted by the shared startup pipeline, valid for any system.
- **System-specific** — interpreted by a specific system's plugin (currently only **C64**).

The **Depends on** column lists each parameter's requirements and any parameters it is mutually
exclusive with. Availability differs between the Avalonia Desktop app and the Headless app where
noted; an *(Avalonia Desktop only)* / *(Headless only)* marker means the flag is ignored by the
other app.

!!! important
    `--script` / `--scriptDir` are **mutually exclusive** with all automated-startup parameters
    (`--system`, `--systemVariant`, `--start`, `--waitForSystemReady`, `--loadPrg`, `--loadPrgUrl`,
    `--runLoadedProgram`, and the C64-specific parameters). In scripting mode the Lua script is
    responsible for all emulator setup and lifecycle; combining the two modes is an error.

### Scripting parameters *(general)*

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--script <path>` | Load and run a Lua script. Can be specified multiple times. | Exclusive with all automated-startup parameters. | `--script scripts/demo.lua` |
| `--scriptDir <path>` | Override the script directory from `appsettings.json`. | Exclusive with all automated-startup parameters. | `--scriptDir ./scripts` |

### System selection & lifecycle *(general)*

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--system <name>` | Select a system (e.g. `C64`, `Generic`). | — | `--system C64` |
| `--systemVariant <name>` | Select a system variant (e.g. `C64PAL`, `C64NTSC`). | Requires `--system`. | `--systemVariant C64PAL` |
| `--start` | Auto-start the emulator after selection. | Requires `--system`. | `--start` |
| `--waitForSystemReady` | Wait until the system reports ready (e.g. C64 BASIC prompt) before continuing. | Requires `--start`. | `--waitForSystemReady` |
| `--loadPrg <path>` | Load a local `.prg` file into memory. | Requires `--start`. Exclusive with `--loadPrgUrl` and C64 `--loadD64` / `--loadD64Url`. | `--loadPrg game.prg` |
| `--loadPrgUrl <url>` | *(Avalonia Desktop only.)* Fetch a `.prg` over HTTP(S) and load it into memory. | Requires `--start`. Exclusive with `--loadPrg` and C64 `--loadD64` / `--loadD64Url`. | `--loadPrgUrl https://example.com/game.prg` |
| `--runLoadedProgram` | Run the loaded program after loading. | Requires `--start` and a load source (`--loadPrg` / `--loadPrgUrl`, or C64 `--loadD64` / `--loadD64Url`). | `--runLoadedProgram` |

!!! note "Loading a C64 `.prg` (BASIC vs machine-language)"
    `--loadPrg` / `--loadPrgUrl` are system-agnostic — they copy the file's bytes to the load
    address in its 2-byte header. No separate parameter is needed for C64 BASIC programs; the C64
    plugin adapts automatically based on the load address:

    - **C64 BASIC program** (load address `$0801`): after the load, the BASIC variable pointers are
      initialized automatically, and with `--runLoadedProgram` the program is started by typing
      `RUN`. This path **requires `--waitForSystemReady`** so the load and `RUN` happen after the
      BASIC prompt is up.
    - **Machine-language program** (any other load address): `--runLoadedProgram` sets the CPU
      program counter to the load address instead.

    So a C64 BASIC `.prg` is loaded and run with
    `--system C64 --start --waitForSystemReady --loadPrg prog.prg --runLoadedProgram`.

### C64-specific parameters *(system-specific — Avalonia Desktop only)*

These extend the general automated-startup flow and are interpreted by the C64 Avalonia shell
plugin. They are **not parsed by the Headless app today** (it has no C64 shell plugin). They build
on the general `--system C64`, `--start`, and `--waitForSystemReady` flags.

#### BASIC source paste

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--basicText <text>` | Paste inline C64 BASIC source (plain text) after BASIC is ready. | Requires `--system C64`, `--start`, `--waitForSystemReady`. Exclusive with `--basicFile`, `--basicUrl`, and any PRG / `.d64` load. | `--basicText "10 print \"hi\":goto 10"` |
| `--basicFile <path>` | Paste C64 BASIC source read from a local file. | Same as `--basicText`. | `--basicFile hello.bas` |
| `--basicUrl <url>` | Fetch C64 BASIC source over HTTP(S) and paste it. | Same as `--basicText`. | `--basicUrl https://example.com/hello.bas` |
| `--runBasic` | Queue `run` + Return after the BASIC source is pasted. | Requires `--basicText` / `--basicFile` / `--basicUrl`. | `--runBasic` |

#### Disk image (`.d64`)

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--loadD64 <path>` | Load a local C64 `.d64` disk image. | Requires `--system C64`, `--start`, `--waitForSystemReady`, and exactly one of `--d64Program` / `--diskMount`. Exclusive with `--loadPrg` / `--loadPrgUrl` / `--loadD64Url`. | `--loadD64 game.d64` |
| `--loadD64Url <url>` | *(Avalonia Desktop only.)* Fetch a `.d64` over HTTP(S). | Same as `--loadD64`. Exclusive with `--loadD64` and the PRG loads. | `--loadD64Url https://example.com/game.d64` |
| `--d64Program <name\|*>` | Direct-load a PRG from the disk image into memory (no disk mount). `*` selects the first directory entry. | Requires `--loadD64` / `--loadD64Url`. Exclusive with `--diskMount`. | `--d64Program "*"` |
| `--diskMount` | Mount the image in drive 8 and prepare `LOAD"*",8,1` + `RUN` via the keyboard buffer. | Requires `--loadD64` / `--loadD64Url`. Exclusive with `--d64Program`. | `--diskMount` |

When loading a `.d64`, the general `--runLoadedProgram` flag controls whether the disk's run
commands are pasted after the load / mount: `LOAD"*",8,1` + `RUN` for `--diskMount`, just `RUN` for
`--d64Program`.

#### Runtime config

These knobs override the C64 host config before the system starts. They apply for **any** C64 start
path — plain `--start`, `--loadPrg` / `--loadPrgUrl`, BASIC paste, or `--loadD64` / `--loadD64Url`.

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--keyboardJoystickEnabled` | Force-enable the C64 keyboard-emulated joystick. | Requires `--system C64`. | `--keyboardJoystickEnabled` |
| `--keyboardJoystickNumber <1\|2>` | C64 joystick port the keyboard emulates (and which gamepad port drives). | Requires `--system C64`. Implies `--keyboardJoystickEnabled`. | `--keyboardJoystickNumber 2` |
| `--audioEnabled <true\|false>` | Override the C64 audio-enable config before start. Omit to keep the existing value. | Requires `--system C64`. | `--audioEnabled false` |

### Logging *(general)*

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--console-log` / `-c` | *(Avalonia Desktop only — Headless always logs to console.)* Enable console logging output. | — | `--console-log` |
| `--log-level <level>` / `-l <level>` | Console log level (`Trace` / `Debug` / `Information` / `Warning` / `Error` / `Critical`). | — | `--log-level Debug` |

### Diagnostics & auto-exit *(general — Avalonia Desktop only)*

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--stats-interval <seconds>` | Log an instrumentation snapshot every N seconds after startup completes. | Requires `--start`. | `--stats-interval 5` |
| `--exit-after <seconds>` | Quit the app N seconds after startup completes. | Requires `--start`. | `--exit-after 60` |

### Debug adapter *(general)*

For the user guide on attaching VS Code, see [Tools / VSCode debugger / Debugging](../tools/vscode-debugger/debugging.md).

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--enableExternalDebug` | Start the VS Code debug adapter (DAP) over TCP. This is the flag that enables the server. | — | `--enableExternalDebug` |
| `--debug-port <port>` | TCP port for the debug adapter (default `6502`). | Requires `--enableExternalDebug`. | `--debug-port 6502` |
| `--debug-bind-address <ip>` | IP address to bind the debug adapter to (default `127.0.0.1`). | Requires `--enableExternalDebug`. | `--debug-bind-address 0.0.0.0` |
| `--debug-wait` | Wait for a debug client to connect before starting. | Requires `--enableExternalDebug`. | `--debug-wait` |

### Remote control *(general)*

For protocol and command list, see [Tools / Remote control](../tools/remote-control/overview.md).

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--remote-port <port>` | Start the TCP remote control server on this port. | — | `--remote-port 6510` |
| `--remote-bind-address <ip>` | IP address to bind the remote control server to (default `127.0.0.1`). | Requires `--remote-port`. | `--remote-bind-address 0.0.0.0` |
| `--allow-remote-quit` | *(Headless only.)* Allow the `emu.quit` remote control command to terminate the app. | Requires `--remote-port`. | `--allow-remote-quit` |
