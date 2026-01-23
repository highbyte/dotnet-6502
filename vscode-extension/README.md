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

1. Open the dotnet-6502 workspace in VSCode
2. Build the debug adapter:
   ```bash
   dotnet build src/apps/Highbyte.DotNet6502.DebugAdapter
   ```
3. Create a launch configuration (or use F5 to auto-generate):
   ```json
   {
     "type": "6502",
     "request": "launch",
     "name": "Debug 6502 Program",
     "program": "${workspaceFolder}/program.prg",
     "stopOnEntry": true
   }
   ```
4. Set breakpoints by using line numbers that correspond to memory addresses
5. Start debugging with F5

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
