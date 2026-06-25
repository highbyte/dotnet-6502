### General parameters *(system-agnostic)*

These are valid for any system.

#### System selection & lifecycle

Desktop equivalents: `--system`, `--systemVariant`, `--start`, `--waitForSystemReady`, `--loadPrg` / `--loadPrgUrl`, `--runLoadedProgram`.

| Query parameter | Description | Depends on | Example |
|---|---|---|---|
| `system` | Select a system (e.g. `C64`, `Generic`). | — | `system=C64` |
| `systemVariant` | Select a system variant (e.g. `C64PAL`, `C64NTSC`). | Requires `system`. | `systemVariant=C64PAL` |
| `start` | Auto-start the selected system. | Requires `system`. | `start=1` |
| `waitForSystemReady` | Wait until the system reports ready (e.g. C64 BASIC prompt). | Requires `system` and `start`. | `waitForSystemReady=1` |
| `loadPrgUrl` | Fetch a `.prg` over HTTP and load it into memory. Relative URLs resolve from the app origin. | Requires `system` and `start`. Exclusive with `loadD64Url` / `loadCrtUrl` / `basicText` / `basicUrl`. | `loadPrgUrl=prg/c64/demo.prg` |
| `runLoadedProgram` | Start executing the loaded program from its load address (or, with `loadD64Url`, paste the disk run commands). | Requires `loadPrgUrl` or `loadD64Url`. | `runLoadedProgram=1` |

`loadPrgUrl` copies the fetched file's bytes to the load address in its 2-byte header — but how a
loaded `.prg` is *interpreted and run* is system-specific. The systems documented below may add
behavior on top of this (e.g. based on the load address); see the relevant system's parameter
group. Cartridge images use the C64-specific `.crt` startup flow instead of PRG loading.

#### Lua scripting *(browser only)*

Desktop equivalents: `--script` (local file, repeatable) / `--scriptDir`. URL-driven Lua is **disabled by default** — enable **Allow URL-driven scripts** in the browser app's general settings, save, then reload.

| Query parameter | Description | Depends on | Example |
|---|---|---|---|
| `script` | Run an inline Lua script. **Base64url-encoded** UTF-8 text. | Exclusive with `scriptUrl` and all system-driven parameters above. Gated by `Scripting.AllowUrlScripts`. | `script=bG9nLmluZm8oJ2hpJyk` |
| `scriptUrl` | Fetch a Lua script from a relative or absolute URL and run it. | Same as `script` (avoids URL-length limits). | `scriptUrl=scripts/demo.lua` |

#### Not available in the browser

Logging, the debug adapter, remote control, and the desktop diagnostics flags (`--stats-interval` / `--exit-after`) have no browser equivalent — the browser sandbox cannot open TCP servers or terminate its own tab. Use the F12 DevTools console for log output.
