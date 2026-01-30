# 6502 Debugger for dotnet-6502

A Visual Studio Code extension for debugging 6502 machine code programs using the dotnet-6502 emulator.

## Features

- **Address-based breakpoints**: Set breakpoints at specific memory addresses
- **Step through instructions**: Step, step in, step out, and continue execution
- **Register inspection**: View CPU registers (PC, SP, A, X, Y) and flags
- **Disassembly view**: See the disassembled instruction at the current PC

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
   - Open a folder that contains a .prg file
   - Create a .vscode/launch.json:
   ```json
   {
     "type": "6502",
     "request": "launch",
     "name": "Debug 6502 Program",
     "program": "${workspaceFolder}/program.prg",
     "stopOnEntry": true
   }
   ```

7. **Start debugging**: Press F5 to start the debugger

8. **Set breakpoints**:
   - Click in the gutter (left margin) of the **Disassembly view** to toggle breakpoints at specific addresses
   - Breakpoints will appear in the BREAKPOINTS panel showing the hex address (e.g., "0x0609")
   
   **Note**: The "+" button in the Breakpoints panel is for function name breakpoints (not applicable for pure assembly debugging). Use the Disassembly view to set instruction breakpoints.

## Configuration

- `program`: Path to the .prg file to debug (required)
- `loadAddress`: Override the load address (optional, normally read from .prg file)
- `stopOnEntry`: Automatically stop after launch (default: true)

## Limitations (MVP)

- No source-level debugging (.asm file support)
- Breakpoints are address-based only (line number = hex address)
- No memory inspection yet
- No conditional breakpoints

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
