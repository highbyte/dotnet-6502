# 6502 Debugger for dotnet-6502

A Visual Studio Code extension for debugging 6502 machine code programs using the dotnet-6502 emulator.

## Features

- **Source-level debugging**: Debug .asm files with ca65 .dbg format support
- **Automatic build task**: Task Provider provides "ca65: build current file (C64)" task (no tasks.json required)
- **Address-based breakpoints**: Set breakpoints at specific memory addresses
- **Source breakpoints**: Set breakpoints in .asm source files (with .dbg file)
- **Step through instructions**: Step, step in, step out, and continue execution
- **Register inspection**: View CPU registers (PC, SP, A, X, Y) and flags
- **Memory viewing**: Inspect memory via Watch panel and Debug Console (e.g., `$0600`, `PC`, `A`)
- **Disassembly view**: See the disassembled instruction at the current PC
- **Out-of-bounds detection**: Warns when execution moves outside source-mapped regions
- **Problem matcher**: Compiler errors appear in Problems panel with inline squiggles

## Requirements

- .NET 10.0 or later
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

6. **In the Extension Development Host window**:
   - Open a folder that contains a .prg file (or .asm source file)
   - Create a .vscode/launch.json:
   ```json
   {
     "type": "dotnet6502",
     "request": "launch",
     "name": "Debug 6502 Program",
     "program": "${workspaceFolder}/program.prg",
     "stopOnEntry": true
   }
   ```
   
   **For source-level debugging with ca65**:
   ```json
   {
     "type": "dotnet6502",
     "request": "launch",
     "name": "Debug with Source",
     "preLaunchTask": "ca65: build current file (C64)",
     "stopOnEntry": true
   }
   ```
   
   The `preLaunchTask` will automatically build your currently open .asm file before debugging. The extension auto-detects the built .prg and .dbg files - **no need to specify program and dbgFile**!
   
   **Important**: Specify the load address in your .asm file:
   ```asm
   .org $0600    ; Set load address
   
   start:
       lda #$01
       ; your code...
   ```

7. **Start debugging**: Press F5 to start the debugger

8. **Set breakpoints**:
   - Click in the gutter (left margin) of the **Disassembly view** to toggle breakpoints at specific addresses
   - Breakpoints will appear in the BREAKPOINTS panel showing the hex address (e.g., "0x0609")
   
   **Note**: The "+" button in the Breakpoints panel is for function name breakpoints (not applicable for pure assembly debugging). Use the Disassembly view to set instruction breakpoints.

## Configuration

### Launch Configuration Parameters

- `program`: Path to the .prg or .bin file to debug (optional when using preLaunchTask - will auto-detect)
- `dbgFile`: Path to ca65 .dbg file for source-level debugging (optional - will auto-detect if using preLaunchTask)
- `loadAddress`: Override the load address (optional, normally read from .prg file or .dbg file)
- `stopOnEntry`: Automatically stop after launch (default: true)
- `stopOnBRK`: Automatically stop when BRK instruction is encountered (default: true)
- `preLaunchTask`: Task to run before launching (e.g., `"ca65: build current file (C64)"`)

### Building Your Code

Use the provided `"ca65: build current file (C64)"` task:

```json
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Debug with Source",
  "preLaunchTask": "ca65: build current file (C64)",
  "stopOnEntry": true
}
```

Make sure your .asm file specifies the load address:
```asm
.org $0600    ; Required: Set load address

start:
    lda #$01
    ; your code...
```

The default task uses `-C c64-asm.cfg` without `--start-addr`. For custom configurations (different config files, optimization, etc.), create a task in `.vscode/tasks.json`:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build with custom config",
      "type": "shell",
      "command": "cl65",
      "args": [
        "-g", "${file}",
        "-o", "${fileBasenameNoExtension}.prg",
        "-C", "custom.cfg",
        "-Wl", "-Ln,${fileBasenameNoExtension}.lbl",
        "-Wl", "--dbgfile,${fileBasenameNoExtension}.dbg",
        "-Wl", "-m,${fileBasenameNoExtension}.map"
      ],
      "problemMatcher": "$ca65"
    }
  ]
}
```

Then use it:
```json
{
  "preLaunchTask": "build with custom config",
  "program": "${workspaceFolder}/program.prg",
  "dbgFile": "${workspaceFolder}/program.dbg",
  "stopOnEntry": true
}
```
  "name": "Debug with preLaunchTask",
  "preLaunchTask": "ca65: build program.asm",
  "program": "${workspaceFolder}/program.prg",
  "dbgFile": "${workspaceFolder}/program.dbg",
  "stopOnEntry": true
}
```

**Default build command**:
```bash
cl65 -g input.asm -o output.prg -C c64-asm.cfg --start-addr 0x0600 \
  -Wl "-Ln,output.lbl" \
  -Wl "--dbgfile,output.dbg" \
  -Wl "-m,output.map"
```

**Benefits**:
- Zero configuration needed
- Quick and simple for standard C64 builds
- Tasks visible in Terminal → Run Task menu

#### Option 3: Custom tasks.json (Maximum control)

For complete control, create your own tasks in `.vscode/tasks.json`. Custom tasks take precedence over auto-generated tasks.

See [TASK_PROVIDER.md](TASK_PROVIDER.md) for more details.

## Limitations

- Conditional breakpoints not yet supported
- Variable inspection limited to registers and memory addresses
- Memory Inspector UI availability varies by VS Code version (use Watch panel as alternative)

## Development

This extension is part of the dotnet-6502 project. To contribute or modify:

1. Open the dotnet-6502 workspace
2. Make changes to the extension or debug adapter
3. Build the debug adapter: `dotnet build`
4. Press F5 in VSCode to launch the extension development host

### Debugging the Debug Adapter and Emulator C# Code

You can debug the C# code (debug adapter and emulator) while debugging a 6502 program using a two-window workflow:

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
