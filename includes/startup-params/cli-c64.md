### C64 parameters *(system-specific — Avalonia Desktop only)*

These extend the general automated-startup flow and are interpreted by the C64 Avalonia shell
plugin. They are **not parsed by the Headless app today** (it has no C64 shell plugin). They build
on the general `--system C64`, `--start`, and `--waitForSystemReady` flags.

!!! note "Loading a C64 `.prg` (BASIC vs machine-language)"
    The general `--loadPrg` / `--loadPrgUrl` flags copy the file's bytes to the load address in its
    2-byte header. No separate parameter is needed for C64 BASIC programs; the C64 plugin adapts
    automatically based on the load address:

    - **C64 BASIC program** (load address `$0801`): after the load, the BASIC variable pointers are
      initialized automatically, and with `--runLoadedProgram` the program is started by typing
      `RUN`. This path **requires `--waitForSystemReady`** so the load and `RUN` happen after the
      BASIC prompt is up.
    - **Machine-language program** (any other load address): `--runLoadedProgram` sets the CPU
      program counter to the load address instead.

    So a C64 BASIC `.prg` is loaded and run with
    `--system C64 --start --waitForSystemReady --loadPrg prog.prg --runLoadedProgram`.

#### BASIC source paste

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--basicText <text>` | Paste inline C64 BASIC source (plain text) after BASIC is ready. | Requires `--system C64`, `--start`, `--waitForSystemReady`. Exclusive with `--basicFile`, `--basicUrl`, and any PRG / `.d64` / `.crt` load. | `--basicText "10 print \"hi\":goto 10"` |
| `--basicFile <path>` | Paste C64 BASIC source read from a local file. | Same as `--basicText`. | `--basicFile hello.bas` |
| `--basicUrl <url>` | Fetch C64 BASIC source over HTTP(S) and paste it. | Same as `--basicText`. | `--basicUrl https://example.com/hello.bas` |
| `--runBasic` | Queue `run` + Return after the BASIC source is pasted. | Requires `--basicText` / `--basicFile` / `--basicUrl`. | `--runBasic` |

#### Disk image (`.d64`)

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--loadD64 <path>` | Load a local C64 `.d64` disk image. ZIP archives are also accepted; by default the first `.d64` entry is used. | Requires `--system C64`, `--start`, `--waitForSystemReady`, and exactly one of `--d64Program` / `--diskMount`. Exclusive with `--loadPrg` / `--loadPrgUrl` / `--loadD64Url` / `--loadCrt` / `--loadCrtUrl`. | `--loadD64 game.d64` |
| `--loadD64Url <url>` | *(Avalonia Desktop only.)* Fetch a `.d64` over HTTP(S). ZIP archives are also accepted; by default the first `.d64` entry is used. | Same as `--loadD64`. Exclusive with `--loadD64`, the PRG loads, and the CRT loads. | `--loadD64Url https://example.com/game.d64` |
| `--loadD64ZipEntry <entry>` | Select an exact `.d64` entry inside a ZIP archive. Use forward slashes for folders. | Requires `--loadD64` / `--loadD64Url` and the source must be a ZIP archive. | `--loadD64ZipEntry side-b/game.d64` |
| `--d64Program <name\|*>` | Direct-load a PRG from the disk image into memory (no disk mount). `*` selects the first directory entry. | Requires `--loadD64` / `--loadD64Url`. Exclusive with `--diskMount`. | `--d64Program "*"` |
| `--diskMount` | Mount the image in drive 8 and prepare `LOAD"*",8,1` + `RUN` via the keyboard buffer. | Requires `--loadD64` / `--loadD64Url`. Exclusive with `--d64Program`. | `--diskMount` |

When loading a `.d64`, the general `--runLoadedProgram` flag controls whether the disk's run
commands are pasted after the load / mount: `LOAD"*",8,1` + `RUN` for `--diskMount`, just `RUN` for
`--d64Program`.

#### Cartridge image (`.crt`)

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--loadCrt <path>` | Attach a local C64 `.crt` cartridge image at startup. ZIP archives containing exactly one `.crt` are also accepted. | Requires `--system C64`, `--start`. `--waitForSystemReady` is not required. Exclusive with PRG, BASIC, and `.d64` startup loads. | `--loadCrt fc3.crt` |
| `--loadCrtUrl <url>` | *(Avalonia Desktop only.)* Fetch and attach a `.crt` over HTTP(S). ZIP archives containing exactly one `.crt` are also accepted. | Same as `--loadCrt`. Exclusive with `--loadCrt`. | `--loadCrtUrl https://example.com/fc3.crt` |
| `--loadCrtZipEntry <entry>` | Select an exact `.crt` entry inside a ZIP archive. Use forward slashes for folders. | Requires `--loadCrt` / `--loadCrtUrl` and the source must be a ZIP archive. Allows archives with multiple `.crt` files when the entry is explicit. | `--loadCrtZipEntry carts/fc3.crt` |

Cartridge startup attaches the image after the C64 instance has been started, and the cartridge
attach operation resets / boots the machine into the cartridge. The general `--runLoadedProgram`
flag does not apply to `.crt` images.

#### Runtime config

These knobs override the C64 host config before the system starts. They apply for **any** C64 start
path — plain `--start`, `--loadPrg` / `--loadPrgUrl`, BASIC paste, `--loadD64` / `--loadD64Url`,
or `--loadCrt` / `--loadCrtUrl`.

| Parameter | Description | Depends on | Example |
|---|---|---|---|
| `--keyboardJoystickEnabled` | Force-enable the C64 keyboard-emulated joystick. | Requires `--system C64`. | `--keyboardJoystickEnabled` |
| `--keyboardJoystickNumber <1\|2>` | C64 joystick port the keyboard emulates (and which gamepad port drives). | Requires `--system C64`. Implies `--keyboardJoystickEnabled`. | `--keyboardJoystickNumber 2` |
| `--audioEnabled <true\|false>` | Override the C64 audio-enable config before start. Omit to keep the existing value. | Requires `--system C64`. | `--audioEnabled false` |
