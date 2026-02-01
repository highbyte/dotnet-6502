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
               "-Wl", "--dbgfile,test-program.dbg"]
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
      "preLaunchTask": "Build test-program.asm (C64)",
      "stopOnEntry": true
    }
  ]
}
```

**Note**: No need to specify `program` or `dbgFile` - they auto-detect from the task output!

## Features

- **Easy setup**: Generate build tasks and launch configs with right-click commands
- **Source-level debugging**: Debug .asm files with ca65 .dbg format support
- **Per-file configuration**: Each .asm file can have its own build task with custom load address
- **Address-based breakpoints**: Set breakpoints at specific memory addresses
- **Source breakpoints**: Set breakpoints in .asm source files (with .dbg file)
- **Step through instructions**: Step, step in, step out, and continue execution
- **Register inspection**: View CPU registers (PC, SP, A, X, Y) and flags
- **Memory viewing**: Inspect memory via Watch panel and Debug Console (e.g., `$c000`, `PC`, `A`)
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

### Launch Configuration Parameters

- `program`: Path to the .prg or .bin file to debug (optional when using preLaunchTask - will auto-detect)
- `dbgFile`: Path to ca65 .dbg file for source-level debugging (optional - will auto-detect if using preLaunchTask)
- `loadAddress`: Override the load address (optional, normally read from .prg file or .dbg file)
- `stopOnEntry`: Automatically stop after launch (default: true)
- `stopOnBRK`: Automatically stop when BRK instruction is encountered (default: true)
- `preLaunchTask`: Task to run before launching (e.g., `"Build test-program.asm (C64)"`)

### Building Your Code

Generate a build task for your .asm file:

1. Right-click the .asm file in Explorer
2. Select **"Generate C64 Build Task (ca65)"**
3. Enter the start/load address (e.g., `0xc000`)
4. Optionally create a launch configuration

Example launch configuration:

```json
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Debug test-program.asm",
  "preLaunchTask": "Build test-program.asm (C64)",
  "stopOnEntry": true
}
```

For more details, see [GENERATE_BUILD_TASK.md](GENERATE_BUILD_TASK.md).

## Limitations

- Conditional breakpoints not yet supported
- Variable inspection limited to registers and memory addresses
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
