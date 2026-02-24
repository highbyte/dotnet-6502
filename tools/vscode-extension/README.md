# 6502 Debugger for dotnet-6502

A Visual Studio Code extension for debugging 6502 machine code programs using the dotnet-6502 emulator.

## Quick Start

### For Source-Level Debugging with ca65

1. **Right-click your .asm file** in Explorer
2. Select **"Generate C64 Build Task (ca65)"**
3. Enter the start address (e.g., `0xc000`)
4. Click **"Create Launch Config"** when prompted
5. Press **F5** to build and debug!

That's it! The extension creates both the build task and launch configuration for you.

### Alternative: Generate Separately

You can also generate the build task and launch configuration separately:

1. Right-click .asm file → **"Generate C64 Build Task (ca65)"** → Enter start address
2. Right-click .asm file → **"Generate Launch Config (DotNet6502)"** → Select the task

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

## Requirements

- .NET 10.0 or later
- cc65 toolchain (for building .asm files)
- The dotnet-6502 project built on your machine

## Usage

1. **Open the vscode-extension folder in VSCode**:
   ```bash
   cd vscode-extension
   code .
   ```

2. **Install dependencies** (first time only):
   ```bash
   npm install
   ```

3. **Compile the extension**:
   ```bash
   npm run compile
   ```

4. **Build the debug adapter** (from repo root):
   ```bash
   cd ../../
   dotnet build src/apps/Highbyte.DotNet6502.DebugAdapter
   ```

5. **Launch Extension Development Host**:
   - In VSCode (with vscode-extension folder open), press **F5**
   - A new "Extension Development Host" window opens with your extension loaded

6. **In the Extension Development Host, open a test folder** with .asm files

7. **Generate build task and launch config**:
   - Right-click an .asm file → **"Generate C64 Build Task (ca65)"**
   - Enter start address (e.g., `0xc000`)
   - Click **"Create Launch Config"** when prompted

8. **Start debugging**: Press **F5** to build and debug

9. **Set breakpoints**:
   - Click in the gutter of your .asm source file (with .dbg support)
   - Or click in the **Disassembly view** to set address breakpoints
   - Right-click a breakpoint → **"Edit Breakpoint..."** to add a condition expression

## Manual Launch Configuration

For debugging pre-built .prg files without source:

```json
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Debug 6502 Program",
  "program": "${workspaceFolder}/program.prg",
  "stopOnEntry": true
}
```

## Configuration

There are three ways to use the debugger, each with different launch.json configurations:

| Mode | `request` | `debugAdapter` | Description |
|------|-----------|----------------|-------------|
| **Launch (minimal)** | `launch` | `minimal` (default) | Launches a standalone 6502 debug adapter. No system emulation — just CPU, memory, and your program. Communicates via STDIO. |
| **Launch (emulator)** | `launch` | `emulator` | Launches an emulator host app (e.g., Avalonia Desktop with C64 emulation), loads your program, and connects the debugger via TCP. |
| **Attach** | `attach` | — | Connects to an already-running emulator host app via TCP. You start the emulator manually with `--enableExternalDebug`. |

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
| `system` | string | `"C64"` | — | Yes | — | System to start in emulator host (e.g., `"C64"`, `"Generic"`). |
| `systemVariant` | string | — | — | Yes | — | System variant (uses first variant if not specified). |
| `startupTimeout` | number | `120` | — | Yes | — | Seconds to wait for emulator host TCP server to start. |
| `waitForSystemReady` | boolean | `true` | — | Yes | — | Wait for system to be fully ready (e.g., C64 BASIC prompt) before connecting. |
| `loadProgram` | boolean | `true` | — | Yes | — | Load the program file into emulator memory. |
| `runProgram` | boolean | `false` | — | Yes | — | Set PC to load address to run program immediately. |

### Example Configurations

**Minimal (standalone debug adapter):**
```json
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Debug 6502 Program",
  "program": "${workspaceFolder}/program.prg",
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
```json
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Launch C64 Emulator",
  "preLaunchTask": "Build test-program.asm",
  "debugAdapter": "emulator",
  "program": "${workspaceFolder}/program.prg",
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
  "debugPort": 6502,
  "stopOnEntry": true
}
```

For attach mode, start the emulator manually first:
```bash
Highbyte.DotNet6502.App.Avalonia.Desktop --enableExternalDebug --debug-port 6502 --system C64 --start
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

### Source-Level Debugging

When debugging with a `.dbg` file (generated by ca65 with `-g` flag), the debugger provides source-level debugging capabilities:

- **Breakpoints**: Set breakpoints in your .asm source files by clicking the gutter
- **Source mapping**: The debugger shows your source code and highlights the current line
- **Step through code**: Step through your assembly instructions with source context

The source mapping is based on the address range of your program as defined in the .dbg file. For example, if your program occupies addresses `$C000-$C01B`, these addresses will map to lines in your .asm file.

### Conditional Breakpoints

Breakpoints can carry an expression that must evaluate to true for the debugger to pause. If the condition is false when the breakpoint address is reached, execution continues without stopping.

**Setting a condition:**

1. Set a breakpoint by clicking the gutter (source view or Disassembly view)
   - Or click the **+** button in the Breakpoints panel to enter a hex address directly
2. **Right-click** the breakpoint dot in the gutter → **"Edit Breakpoint..."**
   - Or right-click the entry in the Breakpoints panel → **"Edit Condition"**
3. Select **"Expression"** and type a condition, then press **Enter**

The breakpoint dot turns **orange** to indicate a conditional breakpoint.

**Expression syntax:**

| Element | Syntax | Example |
|---------|--------|---------|
| Register | `A`, `X`, `Y`, `SP`, `PC` (case-insensitive) | `A == $FF` |
| Status flag | `C`, `Z`, `N`, `V`, `I`, `D`, `B` (0 = clear, 1 = set) | `Z == 1` |
| Memory byte | `[$addr]` | `[$D020] == $01` |
| Indexed memory | `[$addr + reg]` | `[$0300 + X] > $7F` |
| Hex literal | `$hex` or `0xhex` | `$FF`, `0xFF` |
| Decimal literal | digits | `10`, `255` |

Comparison operators: `==`, `!=`, `<`, `<=`, `>`, `>=`

Logical operators: `&&` (and), `||` (or) — evaluated left-to-right, short-circuit

**Examples:**

```
A == $FF                ; Stop only when accumulator is $FF
X >= 10                 ; Stop when X register is 10 or more
Z == 1                  ; Stop when Zero flag is set
C == 0                  ; Stop when Carry flag is clear
[$D020] == $01          ; Stop when border colour register equals 1
[$0300 + X] > $7F       ; Stop when indexed memory byte is > 127
A == $FF && X == 0      ; Both conditions must be true
A == $00 || A == $FF    ; Stop when A is either 0 or 255
PC == $C080             ; Stop when PC reaches a specific address
```

**Notes:**

- Register and flag names are case-insensitive (`a == $ff` is the same as `A == $FF`)
- Flags are treated as integers: `1` = set, `0` = clear
- Memory addresses wrap at the 64 KB boundary
- If the expression cannot be parsed, the debugger always stops (fail-safe)

### Interrupt Handling During Stepping

When single-stepping through code on a system like the C64, hardware interrupts (IRQ/NMI) can fire between any two instructions. Without special handling, pressing F10/F11 could land you inside the Kernal's interrupt service routine — deep in ROM code with no source mapping.

By default, the debugger **automatically skips** these interrupt handlers:

1. After each step, it detects if the CPU entered an IRQ or NMI handler
2. If the handler has **no source mapping** (e.g., ROM code), it sets a temporary breakpoint at the return address and continues execution
3. When the ISR completes (`RTI`), the debugger stops at your original code — as if the interrupt never happened

This makes stepping behave predictably even on interrupt-heavy systems.

**When skipping does NOT apply:**
- If you've loaded debug symbols for the ISR (e.g., Kernal `.dbg` file via `dbgFiles`), the debugger lets you step through it normally
- `BRK` instructions are never skipped (they are treated as software breakpoints, not hardware interrupts)

**To disable:** Set `"skipInterrupts": false` in your launch configuration if you want to step into every interrupt handler regardless of source mapping.

### Stepping Outside Source Code Boundaries

**What happens when execution leaves source-mapped addresses:**

When your program executes code outside the source-mapped address range (e.g., calling into ROM routines via `JSR`), the debugger behavior changes:

1. **Variables View persists**: CPU registers (PC, A, X, Y, SP) and flags remain visible and update normally
2. **Source view disappears**: The editor no longer shows a highlighted line since there's no source mapping
3. **Call Stack shows address**: The call stack displays the memory address and disassembled instruction (e.g., `$C100: c100  0a        ASL A      `)
4. **Warning message**: A console warning indicates you're outside program bounds

**Example scenario:**
```asm
    JSR $C100    ; Stepping into this jumps outside your source code
                 ; $C100 might contain self-modifying code or ROM routine
```

### Using the Disassembly View

**Opening the Disassembly View:**

When stopped at an address without source mapping, you can view the raw disassembly:

1. **Right-click on the Call Stack entry** → Select **"Open Disassembly View"**
2. Or use Command Palette (**Cmd+Shift+P** / **Ctrl+Shift+P**) → **"Debug: Open Disassembly View"**

The Disassembly view shows:
- Memory addresses
- Instruction bytes (hex)
- Disassembled mnemonics
- The current instruction pointer highlighted

**Stepping through disassembly:**

Once the Disassembly view is open, you can continue stepping with **F10** (Step Over) or **F11** (Step Into). The disassembly view will update to show your current position, and the Variables view continues to show register values.

**Returning to source code:**

When execution returns to your source-mapped address range (e.g., after an `RTS` instruction), the editor automatically switches back to showing your .asm source file with the highlighted line.

**Note:** The Debug Adapter Protocol does not (seemingly) provide a way for debug adapters to automatically open the Disassembly view. This is a VS Code UI design decision - users must manually open it the first time they need it. However, once opened, the disassembly view remains visible across debug sessions.

### Memory Inspection

#### Memory Viewer (Primary Method)

The memory viewer opens memory contents in an editor tab with a traditional hex dump format. This is the recommended way to inspect memory ranges:

**How to Open:**
1. **Toolbar button**: Click the array icon ($(symbol-array)) in the debug toolbar during a debug session
2. **Command palette**: Run "View Memory" command

**Usage:**
1. Enter start address (e.g., `0x0000`, `$C000`, `49152`)
   - Default: `0x0000` (start of address space)
   - Supports hex (0x or $) and decimal formats
2. Enter end address (e.g., `0x00FF`, `$C0FF`, `255`)
   - Default: 256 bytes from start address (e.g., `0x00FF` if starting at `0x0000`)
   - Supports hex (0x or $) and decimal formats

**Output Format:**

Memory is displayed in a read-only editor tab titled with the address range (e.g., "0x0000-0x00FF"):
```
0x0000: A9 01 85 00 A9 0A 85 01 A9 00 85 02 A5 00 18 65  ................
0x0010: 01 85 02 E6 03 A5 00 C9 FF D0 F0 A5 02 C9 FF D0  ................
```

Each row shows:
- Memory address (hex)
- 16 bytes in hexadecimal
- ASCII/PETSCII representation (non-printable shown as dots)

**Examples:**
- `0x0000` to `0x00FF` - Zero page (256 bytes)
- `0x0100` to `0x01FF` - Stack area (256 bytes)
- `0xC000` to `0xFFFF` - View entire ROM area on C64 (16KB)
- `0xD000` to `0xD3FF` - VIC-II registers on C64 (1KB)
- `0x0000` to `0xFFFF` - Entire 64KB address space

**Features:**
- No size limits - can view entire 64KB address space
- Results displayed in searchable editor tab
- Tab title shows address range for easy reference
- Addresses respect 64KB boundary (stops at 0xFFFF)

#### Debug Console dump Command (Alternative Method)

For quick memory inspection without opening a new tab, the Debug Console supports a `dump` command:

**Basic Usage:**
```
dump 0xc000 0xc0ff # Dump from $C000 to $C0FF (end address)
dump 0xc000 256    # Dump 256 bytes starting at $C000 (length)
dump 0xc000        # Dump 256 bytes (default length)
```

**Command Aliases:**
- `dump` - Full command name
- `md` - Short alias (memory dump)
- `memdump` - Long alias

**Parameter Formats:**

Second parameter interpretation:
- **Decimal number** → treated as byte length (e.g., `dump 0xc000 256`)
- **Hex format** (0x or $) → treated as end address (e.g., `dump $c000 $c0ff`)

**Examples:**
```
dump 0x0000 256         # Dump zero page and stack area
md fffe 2               # Interrupt vectors
dump $d000 $d3ff        # VIC-II registers on C64
```

#### Debug Console set Command

Modify registers and memory directly from the Debug Console:

```
set A $42           # Set accumulator to $42
set PC $C000        # Set program counter to $C000
set X 10            # Set X register to 10 (decimal)
set $C000 $FF       # Set memory at $C000 to $FF
```

#### Debug Console Expressions

Type expressions directly in the Debug Console to evaluate them:

```
$C000               # Read memory at $C000
PC                  # Show current program counter
A                   # Show accumulator value
screenptr           # Resolve ca65 symbol (if .dbg loaded)
```

### Register and Flag Editing

You can modify CPU registers and flags during debugging:

**In the Variables panel:**
- Double-click any register (PC, A, X, Y, SP) to edit its value
- Double-click any flag (C, Z, I, D, B, V, N) to toggle it (use `0`/`1` or `true`/`false`)
- Values can be entered in hex (`$C000`, `0xC0`) or decimal (`192`)

**In the Debug Console:**
```
set A $42           # Set accumulator to $42
set PC $C000        # Set program counter to $C000
set X 10            # Set X register to 10 (decimal)
set $C000 $FF       # Set memory at $C000 to $FF
```

When you edit the PC register, the editor view automatically updates to show the new location.

### Jump to Line (Set PC)

You can jump the Program Counter to any source line without executing the instructions in between:

1. **Right-click on a line number** or **right-click in the gutter** (to the left of line numbers)
2. Select **"Jump to Line (Set PC)"**

This sets the PC to the address corresponding to that source line. If you click on a non-code line (comment, label, blank line), it automatically snaps to the nearest executable line.

This is available only when the debugger is paused (`debugState == 'stopped'`).

### Inline Address Decorations

When debugging with a `.dbg` file, each source line that maps to a 6502 address shows the address as dim italic text after the line content:

```asm
    LDA #$01        $C000
    STA $D020       $C002
    RTS             $C005
```

For macro body lines (where the address depends on the call site), the decoration updates dynamically on each stop to show the actual address for that specific macro invocation.

### Hover Evaluation

Hover over values in your source code to see their current state:

- **Memory addresses**: Hover `$C000` or `0xC000` → shows the byte value at that address
- **Immediate values**: Hover `#$42` → shows the literal value (not memory contents)
- **Registers**: Hover `A`, `X`, `Y`, `PC`, `SP` → shows current register value
- **ca65 symbols**: Hover a label name (e.g., `screenptr`) → shows the symbol's address and memory contents

### Symbol Resolution

When a `.dbg` file is loaded, ca65 symbols (labels and equates) can be evaluated:

- In the **Debug Console**: Type a symbol name (e.g., `screenptr`) to see its address and value
- In the **Watch panel**: Add symbol names as watch expressions
- On **Hover**: Hover over symbol names in source code

Labels show their address and the memory byte at that address. Equates show their numeric value.

### Multi-File Debug Symbols

For debugging programs that interact with ROM code (e.g., C64 KERNAL), you can merge multiple `.dbg` files:

```json
{
  "type": "dotnet6502",
  "request": "attach",
  "name": "Debug with ROM symbols",
  "preLaunchTask": "Build my-program.asm (C64)",
  "dbgFiles": [
    "${userHome}/path/to/rom.dbg"
  ],
  "stopOnEntry": true
}
```

The primary `.dbg` file is auto-detected from the program path (or specified via `dbgFile`). The `dbgFiles` array adds additional symbol sources that are merged together.

## Limitations

- Variable inspection limited to registers, flags, and memory addresses
- Disassembly view does not open automatically (must be opened manually via right-click on Call Stack)

## How Emulator Mode Works

> **Note:** Currently only the Avalonia Desktop app (`Highbyte.DotNet6502.App.Avalonia.Desktop`) is supported as an emulator host.

When using `"debugAdapter": "emulator"` (launch) or `"request": "attach"`:

1. The emulator host starts with: `--enableExternalDebug --debug-port 6502 --system C64 --start --waitForSystemReady --loadPrg <path>`
2. The emulator starts the specified system (e.g., C64)
3. Waits for the system to be ready (BASIC prompt appears)
4. Loads the PRG file into memory at the address specified in the file
5. Optionally runs the program by setting the CPU PC to the load address
6. VSCode connects the debugger via TCP

In **launch (emulator)** mode, steps 1-5 are handled automatically by the extension. In **attach** mode, you start the emulator manually and the extension only does step 6.

**Note:** Set `runProgram: true` if you want the program to start automatically. Otherwise, the program is loaded but you'll need to manually start it (e.g., `SYS 49152` in C64 BASIC, or step through with the debugger).

## Advanced: Debugging the C# Code

**Window 1 - Extension Test (6502 Debugging)**:
1. Open `/tools/vscode-extension-test/` folder in VS Code
2. Open your 6502 test program (e.g., `test-program.asm`)
3. Press **F5** to start debugging the 6502 program
4. The debug adapter process will start in the background

**Window 2 - Debugging the C# Code**:
1. Open the repository root folder in a separate VS Code window
2. Set breakpoints in the C# code you want to debug:
   - `src/apps/Highbyte.DotNet6502.DebugAdapter/DebugAdapterLogic.cs` - Debug adapter protocol handling
   - `src/libraries/Highbyte.DotNet6502/CPU.cs` - 6502 CPU emulation
   - Any other emulator code
3. Go to Run & Debug → **"Attach to DotNet6502 VSCode Debug Adapter"**
4. Press **F5** to attach to the running debug adapter

Now when you step through 6502 instructions in Window 1, VS Code will hit your C# breakpoints in Window 2, allowing you to:
- See exactly how each 6502 instruction is executed
- Debug the debug adapter protocol implementation
- Step through the emulator's internal logic
- Inspect memory, CPU state, and other internals

**Tips**:
- Set breakpoints in `HandleNextAsync()` or `HandleContinueAsync()` in `DebugAdapterLogic.cs` to catch every step
- Set breakpoints in `CPU.Execute()` to see each 6502 instruction execution
- Use the Variables panel in Window 2 to inspect the emulator's internal state
