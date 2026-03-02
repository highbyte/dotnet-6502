# Testing the 6502 Debugger

## Quick Start

1. **Build the debug adapter:**
   ```bash
   cd ..
   dotnet build src/apps/Highbyte.DotNet6502.DebugAdapter
   ```

2. **Install extension dependencies:**
   ```bash
   cd tools/vscode-extension
   npm install
   npm run compile
   ```

3. **Open the extension in development mode:**
   - Open the `vscode-extension/` folder in VSCode (File > Open Folder)
   - Press F5 (this will launch the Extension Development Host)
   - Or from command line: `code vscode-extension --extensionDevelopmentPath=.`

4. **In the Extension Development Host window:**
   - Open a folder with a .prg file
   - Create a `.vscode/launch.json`:
   ```json
   {
     "version": "0.2.0",
     "configurations": [
       {
         "type": "6502",
         "request": "launch",
         "name": "Debug 6502 Program",
         "program": "${workspaceFolder}/program.prg",
         "stopOnEntry": true
       }
     ]
   }
   ```

5. **Start debugging:**
   - Set breakpoints (line numbers = hex addresses, e.g., line 49152 = 0xc000)
   - Press F5 or use Debug > Start Debugging
   - Use the debug controls to step through code
   - Inspect registers in the Variables panel

## Creating a Test Program

If you need a .prg file to test with, you can:

1. Assemble `test-program.asm` using a 6502 assembler like [dasm](https://github.com/dasm-assembler/dasm):
   ```bash
   dasm test-program.asm -otest-program.prg
   ```

2. Or use an existing .prg file from the samples directory in the main project

## Debugging Features

### Breakpoints
- Set by clicking in the gutter at a line number
- Line number represents the memory address
- Example: Line 49152 = address 0xc000

### Stepping
- **Step Over (F10)**: Execute one instruction
- **Step Into (F11)**: Same as step over (no subroutine context in MVP)
- **Continue (F5)**: Run until breakpoint or BRK

### Variable Inspection
- **Registers**: PC, SP, A, X, Y
- **Flags**: N, V, B, D, I, Z, C
- View in the Variables panel while debugging

### Call Stack
- Shows disassembled instruction at current PC
- Format: `[address] [bytes] [instruction]`
