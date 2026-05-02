# 6502 debugger for dotnet-6502

A Visual Studio Code extension for debugging 6502 assembly source and machine code programs using the `dotnet-6502` emulator from <https://github.com/highbyte/dotnet-6502>.

> _In the `dotnet-6502` emulator there is also a built-in simpler [machine code monitor](https://highbyte.github.io/dotnet-6502/docs/desktop-apps/console-monitor/) in the emulator itself that can be activated with F12 (or pressing the Monitor button). It has less features than this VS Code extension._

## Installing the VSCode extension from the Marketplace

The easiest way to install is directly from the [Visual Studio Code Marketplace](https://marketplace.visualstudio.com/items?itemName=highbyte.dotnet-6502-debugger):

1. Open VS Code
2. Go to the Extensions view (`Ctrl+Shift+X` / `Cmd+Shift+X`)
3. Search for `dotnet-6502-debugger`
4. Click **Install**

Or via command line:
```bash
code --install-extension highbyte.dotnet-6502-debugger
```

## Requirements

The debugger extension connects to a debugger adapter in the dotnet-6502 emulator Avalonia UI desktop app.

Depenencies
- Required: [**`dotnet-6502`**](https://highbyte.github.io/dotnet-6502/docs/desktop-apps/installation/#install-via-package-manager) emulator Avalonia UI desktop app.
- Optional: [**`cc65`**](https://github.com/cc65/cc65) compiler/assembler toolchain for source debugging.

> If a dependency is missing, the extension will detect it and offer to run the install command for you.

The **dotnet-6502** emulator can be installed via a package manager.

_macOS (Homebrew)_:
```bash
brew tap highbyte/dotnet-6502 && brew install --cask dotnet-6502
```
_Linux (Homebrew)_:
```bash
brew tap highbyte/dotnet-6502 && brew install --formula dotnet-6502
```
_Windows (Scoop)_:
```powershell
scoop bucket add dotnet-6502 https://github.com/highbyte/scoop-dotnet-6502 && scoop install dotnet-6502
```

For source debugging the [`cc65`](https://github.com/cc65/cc65) toolchain is required for building source files to binaries and `.dgb` debug symbols.

_macOS (Homebrew)_:
```bash
brew install cc65
```

_Windows and Linux (manual)_:
  - See [cc65 getting started](https://cc65.github.io/getting-started.html)
  - The `ca65` and `cl65` executables must be in the system PATH.


## Debug Quick Start

### For Source-Level Debugging with ca65 assembler for a C64 program

1. **Right-click your .asm file** in Explorer
2. Select **"Generate C64 Build Task (ca65)"**
3. Enter the start address (e.g., `0xc000`)
4. Click **"Create Launch Config"** when prompted
5. Press **F5** to build and debug!

That's it! The extension creates both the build task and launch configuration for you.

### Alternative: Generate Separately

You can also generate the build task and launch configuration separately:

1. Right-click .asm file → **"Generate C64 Build Task (ca65)"** → Enter start address
2. Right-click .prg file → **"Generate Launch Config for C64 emulator (DotNet6502)"** → Select the task

### Example Configuration

The generated files look like this:

**`.vscode/tasks.json`**:
```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "Build test-program.asm (C64)",
      "type": "shell",
      "command": "cl65",
      "args": ["-g", "test-program.asm", "-o", "test-program.prg",
               "-C", "c64-asm.cfg", "--start-addr", "0xc000",
               "-Wl", "-Ln,test-program.lbl",
               "-Wl", "--dbgfile,test-program.dbg",
               "-Wl", "-m,test-program.map"],
      "problemMatcher": "$ca65"
    }
  ]
}
```

**`.vscode/launch.json`**:
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "type": "dotnet6502",
      "request": "launch",
      "name": "Debug test-program.asm",
      "preLaunchTask": "Build test-program.asm",
      "stopOnEntry": true
    }
  ]
}
```

**Note**: No need to specify `program` or `dbgFile` - they auto-detect from the task output!

## Features

- **Easy setup**: Generate build tasks and launch configs with right-click commands
- **Source-level debugging**: Debug .asm files with ca65 .dbg format support
- **Multi-file debug symbols**: Merge multiple .dbg files (e.g., your program + C64 ROM symbols) via `dbgFiles`
- **Per-file configuration**: Each .asm file can have its own build task with custom load address
- **Address-based breakpoints**: Set breakpoints at specific memory addresses
- **Source breakpoints**: Set breakpoints in .asm source files (with .dbg file)
- **Conditional breakpoints**: Stop only when a register, flag, or memory condition is true
- **Logpoints**: Log messages to the debug console without stopping execution
- **Step through instructions**: Step, step in, step out, and continue execution
- **Register inspection**: View CPU registers (PC, SP, A, X, Y) and flags in Variables panel
- **Register and flag editing**: Double-click registers or flags in the Variables panel to modify values
- **Jump to Line (Set PC)**: Right-click a line number or gutter to set the Program Counter to that line
- **Inline address decorations**: Source lines show their mapped 6502 address (`$XXXX`) as dim inline text
- **Hover evaluation**: Hover over addresses, registers, or ca65 symbols in source code to see values
- **Memory viewing**: Inspect memory via Watch panel and Debug Console (e.g., `$c000`, `PC`, `A`)
- **Memory viewer**: View memory ranges in hex dump format with toolbar button or command palette
- **Debug Console commands**: `dump`/`md` for memory dumps, `set` for modifying registers/memory
- **Disassembly view**: See the disassembled instruction at the current PC
- **Problem matcher**: Compiler errors appear in Problems panel with inline squiggles



## Usage

### Source debugging
1. **In the Extension Development Host, open a test folder** with .asm files

2. **Generate build task and launch config**:
   - Right-click an .asm file → **"Generate C64 Build Task (ca65)"**
   - Enter start address (e.g., `0xc000`)
   - Click **"Create Launch Config"** when prompted
   - Note: For more info on the Build task, see [here](GENERATE_BUILD_TASK.md)

3. **Start debugging**: Press **F5** to build and debug

4. **Set breakpoints**:
   - Click in the gutter of your .asm source file (with .dbg support)
   - Right-click a breakpoint → **"Edit Breakpoint..."** to add a condition expression

### Disassembly debugging
1. **In the Extension Development Host, open a test folder** with .prg (or other binary) files

2. **Create launch config**:
For full emulator debugging of a C64 .prg program, there is built-in option to create a launch.json configuration.
- Right-click an .prg file → **"Generate Launch Config for C64 emulator (DotNet6502)"**

Or create a manual launch.json configuration for other scenarios (see below).

3. **Start debugging**: Press **F5** to build and debug

4. **Set breakpoints**:
- Manually add a breakpoint at an address via the + sign in the **Breakpoints** section
- Or click in gutter of the **Disassembly view** to toggle address breakpoints
- Right-click a breakpoint → **"Edit Breakpoint..."** to add a condition expression

5. **Open Disassembly view**:
After pausing execution in VSCode debugger, or a breakpoint is hit, the Disassembly view (with the current instruction) may not be shown by default. If that's the case, expand the **Call stack** section in the right pane, right click the single entry and select **Open Dissassembly View**.  

## Manual Launch Configuration

Example for debugging pre-built .prg files without source. See full configuration reference below for all options.

> **Note:** `program.prg` in the examples below is a placeholder — replace it with the actual path to your `.prg` file.

```jsonc
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Debug 6502 Program",
  "program": "${workspaceFolder}/program.prg", // replace with your actual .prg file
  "stopOnEntry": true
}
```

## Configuration

There are three ways to use the debugger, each with different launch.json configurations:

| Mode | `request` | `debugAdapter` | Description |
|------|-----------|----------------|-------------|
| **Launch (minimal)** | `launch` | `minimal` (default) | Launches a standalone 6502 debug adapter. No system emulation — just CPU, memory, and your program. Communicates via STDIO. |
| **Launch (emulator)** | `launch` | `emulator` | Launches an emulator host app (e.g., Avalonia Desktop with C64 emulation), loads your program, and connects the debugger via TCP. |
| **Attach** | `attach` | — | Connects to an already-running emulator host app via TCP. Start the emulator manually via command line (`--enableExternalDebug`, optional `--debug-bind-address <ip>`) or via the **VSCode Debug Server** toggle in the Avalonia app's Debug tab. |

### Launch Configuration Parameters

| Parameter | Type | Default | Launch (minimal) | Launch (emulator) | Attach | Description |
|-----------|------|---------|:-:|:-:|:-:|-------------|
| `program` | string | — | Yes | Yes | Optional | Path to .prg file. In launch modes, this is the program to load/debug. In attach mode, only used to auto-detect the `.dbg` file path — not needed if `dbgFile` or `preLaunchTask` is specified. Optional with `preLaunchTask` (auto-detected from task output). |
| `dbgFile` | string | — | Yes | Yes | Yes | Path to ca65 .dbg file for source-level debugging. Auto-detected from `program` path or `preLaunchTask` output. |
| `dbgFiles` | string[] | — | Yes | Yes | Yes | Additional .dbg files to merge for multi-component source debugging (e.g., ROM debug symbols alongside your program's .dbg file). If `dbgFile` is omitted, the first entry becomes the primary. |
| `loadAddress` | number | — | Yes | — | — | Override load address (normally read from .prg file header). |
| ` ` | boolean | `true` | Yes | Yes | Yes | Stop at first instruction executed after after launch/attach. |
| `stopOnBRK` | boolean | `true` | Yes | Yes | Yes | Stop when BRK instruction ($00) is encountered. |
| `skipInterrupts` | boolean | `true` | Yes | Yes | Yes | When stepping (F10/F11), automatically skip over hardware interrupt handlers (IRQ/NMI) that have no source mapping. Prevents stepping into ROM ISR code during single-step debugging. |
| `preLaunchTask` | string | — | Yes | Yes | Yes | VSCode task to run before launching/attaching (e.g., build task). When used with attach, the program and .dbg file paths are auto-detected from task output. |
| `debugAdapter` | string | `"minimal"` | Yes | Yes | — | `"minimal"` for standalone adapter, `"emulator"` to launch emulator host app. |
| `emulatorExecutable` | string | *(auto)* | Yes | Yes | — | Executable path or name. Defaults to `Highbyte.DotNet6502.DebugAdapter.ConsoleApp` (minimal) or `Highbyte.DotNet6502.App.Avalonia.Desktop` (emulator). Resolved via PATH, then repo build output. |
| `debugPort` | number | `6502` | — | Yes | Yes | TCP port for debug adapter communication. |
| `debugHost` | string | `"127.0.0.1"` | — | Yes | Yes | TCP host or IP address the extension connects to. Most useful for attach mode; leave it at loopback for locally launched emulator sessions unless the emulator is intentionally listening on another interface. |
| `system` | string | `"C64"` | — | Yes | — | System to start in emulator host (e.g., `"C64"`, `"Generic"`). |
| `systemVariant` | string | — | — | Yes | — | System variant (uses first variant if not specified). |
| `startupTimeout` | number | `120` | — | Yes | — | Seconds to wait for emulator host TCP server to start. |
| `waitForSystemReady` | boolean | `true` | — | Yes | — | Wait for system to be fully ready (e.g., C64 BASIC prompt) before connecting. |
| `loadProgram` | boolean | `true` | — | Yes | — | Load the program file into emulator memory. |
| `runProgram` | boolean | `false` | — | Yes | — | Set PC to load address to run program immediately. |

### Example `launch.json` Configurations

**Minimal (standalone debug adapter):**
```jsonc
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Debug 6502 Program",
  "program": "${workspaceFolder}/program.prg", // replace with your actual .prg file
  "stopOnEntry": true
}
```

**Minimal with build task (source-level debugging):**
```json
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Debug test-program.asm",
  "preLaunchTask": "Build test-program.asm",
  "stopOnEntry": true
}
```

**Emulator (launch Avalonia Desktop with C64) with .asm source level debugging:**
```json
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Launch C64 Emulator",
  "debugAdapter": "emulator",
  "system": "C64",
  "debugPort": 6502,
  "stopOnEntry": true,
  "loadProgram": true,
  "runProgram": true
}
```

**Emulator (launch Avalonia Desktop with C64) with .prg binary:**
```jsonc
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Launch C64 Emulator",
  "preLaunchTask": "Build test-program.asm",
  "debugAdapter": "emulator",
  "program": "${workspaceFolder}/program.prg", // replace with your actual .prg file, or omit if preLaunchTask auto-detects it
  "system": "C64",
  "debugPort": 6502,
  "stopOnEntry": true,
  "loadProgram": true,
  "runProgram": false
}
```

**Attach (connect to already-running emulator, no source debugging):**
```json
{
  "type": "dotnet6502",
  "request": "attach",
  "name": "Attach to Emulator",
  "debugHost": "127.0.0.1",
  "debugPort": 6502,
  "stopOnEntry": true
}
```

For attach mode, enable the TCP debug server in the emulator first — there are two ways. The emulator supports a configurable bind address (`--debug-bind-address <ip>` or the **Bind** field in the UI), and the extension supports a matching `debugHost` setting. Both default to `127.0.0.1`, which remains the right default for local debugging. Set both explicitly only when you intentionally want VS Code to connect to another machine, container, VM, or non-loopback local interface.

**Option A — Via the Avalonia app UI** (recommended):
1. Start `Highbyte.DotNet6502.App.Avalonia.Desktop` normally (no extra arguments needed)
2. Start a system (e.g. C64) from the emulator
3. Go to the **Debug & Remoting** tab in the Information Area (middle column)
4. In the **VSCode Debug Server** section, keep **Bind** at `127.0.0.1` for local VS Code attach, set the port (default: `6502`), and click **Start**
5. Press **F5** in VSCode to attach

**Option B — Via command line**:
```bash
Highbyte.DotNet6502.App.Avalonia.Desktop --enableExternalDebug --debug-port 6502 --debug-bind-address 127.0.0.1 --system C64 --start
```

Remote attach example:
```json
{
  "type": "dotnet6502",
  "request": "attach",
  "name": "Attach to remote emulator",
  "debugHost": "192.168.1.50",
  "debugPort": 6502,
  "stopOnEntry": true
}
```

**Attach with build task (auto-detect .dbg file for source debugging):**
```json
{
  "type": "dotnet6502",
  "request": "attach",
  "name": "Attach to Emulator with Source Debug",
  "preLaunchTask": "Build test-program.asm (C64)",
  "stopOnEntry": true
}
```

**Attach with explicit .dbg file:**
```json
{
  "type": "dotnet6502",
  "request": "attach",
  "name": "Attach with Source Debug",
  "dbgFile": "${workspaceFolder}/program.dbg",
  "stopOnEntry": true
}
```

**Attach with additional ROM debug symbols:**
```json
{
  "type": "dotnet6502",
  "request": "attach",
  "name": "Attach with ROM Symbols",
  "preLaunchTask": "Build test-program.asm (C64)",
  "dbgFiles": [
    "${userHome}/path/to/rom.dbg"
  ],
  "stopOnEntry": true
}
```

### Building Your Code

Generate a build task for your .asm file:

1. Right-click the .asm file in Explorer
2. Select **"Generate C64 Build Task (ca65)"**
3. Enter the start/load address (e.g., `0xc000`)
4. Optionally create a launch configuration

For more details, see [GENERATE_BUILD_TASK.md](GENERATE_BUILD_TASK.md).

## Debugging Behavior

For a full reference on conditional breakpoints, logpoints, hit counts, source-line stepping, interrupt handling, disassembly, memory inspection, register editing, and more, see [DEBUGGING.md](DEBUGGING.md).

For cross-machine debugging (path mappings, remote source fallback, SSH tunnels), see [REMOTE_DEBUGGING.md](REMOTE_DEBUGGING.md).

## Limitations

- Variable inspection limited to registers, flags, and memory addresses
- Disassembly view does not open automatically (must be opened manually via right-click on Call Stack)

## How Emulator Mode Works

> **Note:** Currently only the Avalonia Desktop app (`Highbyte.DotNet6502.App.Avalonia.Desktop`) is supported as an emulator host.

When using `"debugAdapter": "emulator"` (launch) or `"request": "attach"`:

1. The emulator host starts with: `--enableExternalDebug --debug-port 6502 --system C64 --start --waitForSystemReady --loadPrg <path>`
   - The emulator defaults the TCP debug bind address to `127.0.0.1`.
   - The extension defaults `debugHost` to `127.0.0.1` too, but you can override it for remote attach scenarios.
2. The emulator starts the specified system (e.g., C64)
3. Waits for the system to be ready (BASIC prompt appears)
4. Loads the PRG file into memory at the address specified in the file
5. Optionally runs the program by setting the CPU PC to the load address
6. VSCode connects the debugger via TCP

In **launch (emulator)** mode, steps 1-5 are handled automatically by the extension. In **attach** mode, you start the emulator manually (or use the UI toggle — see [Attach mode examples above](#example-launchjson-configurations)) and the extension only does step 6.

**Note:** Set `runProgram: true` if you want the program to start automatically. Otherwise, the program is loaded but you'll need to manually start it (e.g., `SYS 49152` in C64 BASIC, or step through with the debugger).

## Development

For building, running, and debugging the extension from source, see [DEVELOPMENT.md](DEVELOPMENT.md).

## Known Issues

See [KNOWN-ISSUES.md](https://github.com/highbyte/dotnet-6502/blob/master/tools/vscode-extension/KNOWN-ISSUES.md) for a list of known issues and limitations.
