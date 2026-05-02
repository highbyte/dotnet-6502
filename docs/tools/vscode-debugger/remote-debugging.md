# Remote debugging

This document explains how to use the `dotnet6502` VS Code debugger to debug 6502 programs running on a **remote machine** — that is, a machine where the emulator runs but that has a different filesystem layout than the VS Code machine.

See [Debugging](debugging.md) for background on the TCP debug adapter architecture.

---

## Supported scenarios

| Scenario | `request` | `debugHost` | `pathMappings` | Debug symbols on |
|----------|-----------|-------------|----------------|------------------|
| Local launch (minimal STDIO) | `launch` | n/a | n/a | local |
| Local launch (emulator TCP, VS Code spawns) | `launch` | `127.0.0.1` | n/a | local |
| Local attach (manually started emulator) | `attach` | `127.0.0.1` | n/a | local |
| **Remote attach** | `attach` | remote IP | yes | remote machine |

**Launch mode is always local.** VS Code spawns the emulator as a child process, so the filesystem is shared automatically.

**Remote attach** is the scenario described in this document.

---

## Remote attach: how it works

Two complementary mechanisms provide source-level debugging across machines:

### 1. Path mappings (`pathMappings`)

The primary mechanism. Translates absolute paths in DAP messages between the local (VS Code) machine and the remote (debug adapter) machine — the same pattern used by Node.js, Python, C#, and Go debuggers.

```json
"pathMappings": [
  {
    "localRoot": "${workspaceFolder}",
    "remoteRoot": "/home/ubuntu/project"
  }
]
```

- **Remote → local** (adapter response → VS Code): stack frame paths, breakpoint confirmations.
- **Local → remote** (VS Code request → adapter): breakpoint locations set by the user.
- Longest-prefix match wins when multiple entries are configured.
- On Windows, drive letters are compared case-insensitively; separators are normalised.

### 2. Remote source fallback (`useRemoteSources`)

When a stack frame's source path has **no local mapping** (e.g. the file is outside the mapped root, or no `pathMappings` are configured at all), VS Code uses the DAP `source` request to fetch the file content from the remote adapter. The file is shown as a read-only virtual document in the editor.

This is a standard DAP feature — no protocol extensions. It is enabled by default (`useRemoteSources: true`) for attach configurations.

**Breakpoints still work** in these virtual documents because the `source.path` round-trips from the adapter (the adapter always sees its own remote path).

---

## File location requirements

The `.dbg` file **must always exist on the remote machine** — the debug adapter reads it directly from disk to build the address→source mapping. Without it the adapter cannot resolve any PC address to a source file or line number, and you will only see disassembly.

The `.asm` source files are a separate concern: they need to be reachable by whichever mechanism resolves them for display in VS Code.

| `.dbg` on remote | `.asm` on local | `.asm` on remote | Mechanism | Result |
|:---:|:---:|:---:|---|---|
| ✓ | ✓ | optional | `pathMappings` | VS Code opens local file directly — full editor experience |
| ✓ | — | ✓ | `useRemoteSources` | Adapter serves file content on demand; shown as read-only virtual document |
| ✓ | ✓ | ✓ | both | Local file used when path matches; remote fallback for anything outside the mapped root |
| ✓ | — | — | neither | Disassembly view only — no source lines |

**`pathMappings`** requires the `.asm` files to be present on the **local** machine (VS Code opens them directly by translated path).

**`useRemoteSources`** requires the `.asm` files to be present on the **remote** machine (the adapter reads them from its own disk and sends the content to VS Code).

The typical way to satisfy the `.dbg` (and `.prg`) requirement is to build on the remote machine, or `rsync`/`scp` the build artifacts up before attaching.

---

## Quickstart

### Step 1 — Build on the remote machine

Build your `.prg` and `.dbg` files on the remote machine (or `rsync`/`scp` them up before debugging). The debug adapter reads these files from its own disk.

```sh
# On the remote machine
ca65 -g myprog.asm -o myprog.o
ld65 -C c64-asm.cfg myprog.o -o myprog.prg -Wl --dbgfile,myprog.dbg
```

### Step 2 — Start the emulator on the remote machine

Start the emulator with external debug enabled, binding to all interfaces so VS Code can connect from another machine:

```sh
dotnet-6502 --enableExternalDebug \
            --debug-bind-address 0.0.0.0 \
            --debug-port 6502 \
            --system C64 \
            --start
```

!!! warning "Security note"
    Binding to `0.0.0.0` exposes the debug port on the network. Use an SSH tunnel (`ssh -L 6502:localhost:6502 user@remote`) if you prefer to keep `debugHost` as `127.0.0.1` and avoid exposing the port.

### Step 3 — Configure VS Code

Use the Command Palette → **Generate Remote Attach Config (dotnet-6502)** to generate a pre-filled `launch.json` entry. You will be prompted for:

- Remote host IP or hostname
- Remote debug port (default 6502)
- Remote project root directory
- Remote `.dbg` file path (optional)

Or write the config manually:

```json
{
  "type": "dotnet6502",
  "request": "attach",
  "name": "Remote Attach to Emulator (dotnet-6502)",
  "debugHost": "192.168.1.100",
  "debugPort": 6502,
  "dbgFile": "/home/ubuntu/project/myprog.dbg",
  "pathMappings": [
    {
      "localRoot": "${workspaceFolder}",
      "remoteRoot": "/home/ubuntu/project"
    }
  ],
  "useRemoteSources": true,
  "stopOnEntry": true
}
```

### Step 4 — Attach and debug

Press **F5** (or select the configuration and click the green play button). VS Code connects to the remote emulator. Source files are resolved via `pathMappings`; any file outside the mapped root is fetched on-demand as a virtual read-only document.

---

## Configuration reference

### `pathMappings`

*(attach only)* Array of `{ localRoot, remoteRoot }` pairs. The extension translates paths in DAP messages before they reach VS Code or the adapter. Entries are matched by longest prefix; separators and drive-letter case are normalised automatically.

```json
"pathMappings": [
  { "localRoot": "${workspaceFolder}/src", "remoteRoot": "/home/ubuntu/project/src" },
  { "localRoot": "${workspaceFolder}",     "remoteRoot": "/home/ubuntu/project" }
]
```

### `useRemoteSources`

*(attach only, default `true`)* When `true`, source files that have no local mapping are fetched from the remote adapter via the DAP `source` request and shown as read-only virtual documents. Set to `false` to disable and fall back to disassembly-only view.

### `dbgFile`

*(attach mode)* Path to the `.dbg` file **on the debug adapter machine** (not the local VS Code workspace). Required for source-level debugging. Example: `/home/ubuntu/project/myprog.dbg`.

### `debugHost`

*(attach mode)* IP address or hostname of the remote machine running the emulator. For cross-machine connections, the emulator must be started with `--debug-bind-address 0.0.0.0`.

---

## Using an SSH tunnel

If you cannot or do not want to expose the debug port, forward it over SSH:

```sh
ssh -L 6502:localhost:6502 user@remote-machine
```

Then set `"debugHost": "127.0.0.1"` in your launch config. The SSH process keeps the tunnel open; kill it when you are done debugging.

---

## Alternative: VS Code Remote-SSH

If you already develop on the remote machine via the [Remote - SSH extension](https://code.visualstudio.com/docs/remote/ssh), the entire VS Code workspace runs on the remote machine and the `dotnet6502` debugger operates as a local debugger from its own perspective. In that case:

- No `pathMappings` needed — paths are already identical.
- Use a normal local `attach` config pointing to `127.0.0.1`.
- Remote-SSH is complementary, not a replacement — it doesn't cover emulator-on-different-OS, locked-down targets, or multiple developers sharing one emulator.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| "Connection refused" | Emulator not started with `--enableExternalDebug` | Start with `--enableExternalDebug --debug-bind-address 0.0.0.0` |
| Source not found, disassembly view | `pathMappings` don't cover the file | Add a mapping or enable `useRemoteSources` |
| Breakpoints not binding | Wrong `remoteRoot` prefix | Check adapter log for the actual path in stack frames; adjust `remoteRoot` |
| Read-only virtual document shown instead of local file | File is outside all `localRoot` prefixes | Extend `pathMappings` to cover the file |
