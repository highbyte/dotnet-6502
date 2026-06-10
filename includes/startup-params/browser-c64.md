### C64 parameters *(system-specific)*

These are interpreted by the C64 plugin and build on the general `system=C64`, `start`, and `waitForSystemReady` parameters.

!!! note "Loading a C64 `.prg` (BASIC vs machine-language)"
    The general `loadPrgUrl` parameter copies the fetched file's bytes to the load address in its
    2-byte header. No separate parameter is needed for C64 BASIC programs; the C64 plugin adapts
    automatically based on the load address:

    - **C64 BASIC program** (load address `$0801`): after the load, the BASIC variable pointers are
      initialized automatically, and with `runLoadedProgram` the program is started by typing `RUN`.
      This path **requires `waitForSystemReady`** so the load and `RUN` happen after the BASIC
      prompt is up.
    - **Machine-language program** (any other load address): `runLoadedProgram` sets the CPU program
      counter to the load address instead.

    So a C64 BASIC `.prg` is loaded and run with
    `?system=C64&start=1&waitForSystemReady=1&loadPrgUrl=prg/c64/prog.prg&runLoadedProgram=1`.

#### BASIC source paste

Desktop equivalents: `--basicText` (plain text), `--basicFile` (local file), `--basicUrl`, `--runBasic`.

| Query parameter | Description | Depends on | Example |
|---|---|---|---|
| `basicText` | Paste inline C64 BASIC source. **Base64url-encoded** UTF-8 text. | Requires `system=C64`, `start`, `waitForSystemReady`. Exclusive with `basicUrl`, `loadPrgUrl`, `loadD64Url`. | `basicText=MTAgcHJpbnQ...` |
| `basicUrl` | Fetch C64 BASIC source text over HTTP and paste it. | Same as `basicText`. | `basicUrl=basic/c64/hello-world.bas` |
| `runBasic` | Queue `RUN` after the BASIC source is pasted. | Requires `basicText` or `basicUrl`. | `runBasic=1` |

#### Disk image (`.d64`)

Desktop equivalents: `--loadD64` (local file) / `--loadD64Url` (URL), `--d64Program`, `--diskMount`. Bytes are fetched **after** the C64 has booted to BASIC ready, so the user sees the live BASIC prompt during the download (no blank page).

| Query parameter | Description | Depends on | Example |
|---|---|---|---|
| `loadD64Url` | Fetch a C64 `.d64` disk image over HTTP. | Requires `system=C64`, `start`, `waitForSystemReady`, and exactly one of `d64Program` / `diskMount`. Exclusive with `loadPrgUrl` / `basicText` / `basicUrl`. | `loadD64Url=d64/game.d64` |
| `d64Program` | Direct-load a PRG from the image into memory (no disk mount). `*` selects the first directory entry; URL-encode names with spaces. | Requires `loadD64Url`. Exclusive with `diskMount`. | `d64Program=*` |
| `diskMount` | Mount the image in drive 8 and prepare `LOAD"*",8,1` + `RUN`. | Requires `loadD64Url`. Exclusive with `d64Program`. | `diskMount=1` |

With `loadD64Url`, `runLoadedProgram` pastes the disk's run commands after load / mount: `LOAD"*",8,1` + `RUN` for `diskMount`, just `RUN` for `d64Program`.

#### Runtime config

Desktop equivalents: `--keyboardJoystickEnabled`, `--keyboardJoystickNumber`, `--audioEnabled`. These apply for **any** C64 start path (plain `start`, `loadPrgUrl`, BASIC paste, `loadD64Url`).

| Query parameter | Description | Depends on | Example |
|---|---|---|---|
| `keyboardJoystickEnabled` | Force-enable the C64 keyboard-emulated joystick before the system starts. | Requires `system=C64`. | `keyboardJoystickEnabled=1` |
| `keyboardJoystickNumber` | C64 joystick port the keyboard emulates (and which gamepad port drives). Must be `1` or `2`. | Requires `system=C64`. Implies `keyboardJoystickEnabled`. | `keyboardJoystickNumber=2` |
| `audioEnabled` | Override C64 audio enable before the system starts (`true` / `false`). | Requires `system=C64`. | `audioEnabled=false` |
