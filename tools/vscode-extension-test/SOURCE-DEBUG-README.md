# Source-Level Debugging with ca65

This document explains how to use source-level debugging with the dotnet-6502 VS Code debugger extension using ca65 debug symbols.

## Overview

The debugger now supports **source-level debugging** using ca65 `.dbg` files. When debug symbols are loaded:
- Set breakpoints in your `.asm` source files
- See source lines in the call stack
- Step through your original assembly source code

## Requirements

- **ca65/cc65 toolchain** installed (for assembling with debug info)
- Your source code assembled with debug information

## Quick Start

### 1. Assemble with Debug Information

When assembling your code with ca65, use the `--debug-info` flag:

```bash
# Assemble with debug info
ca65 test-program.asm -o test-program.o --debug-info

# Link (this will create test-program.dbg automatically)
ld65 -C example.cfg -o test-program.prg test-program.o -Ln test-program.lbl
```

The `--debug-info` flag tells ca65 to generate debug information, and ld65 will create a `.dbg` file with the same base name as your output file.

### 2. Configure launch.json

Add the `dbgFile` parameter to your debug configuration:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "type": "dotnet6502",
      "request": "launch",
      "name": "Debug with Source",
      "program": "${workspaceFolder}/test-program.prg",
      "dbgFile": "${workspaceFolder}/test-program.dbg",
      "stopOnEntry": true,
      "stopOnBRK": true
    }
  ]
}
```

### 3. Debug Your Source

1. Open your `.asm` file in VS Code
2. Set breakpoints by clicking in the gutter (left margin)
3. Press F5 to start debugging
4. The debugger will:
   - Show your source file with the current line highlighted
   - Allow you to set breakpoints on source lines
   - Step through your original source code

## Example

See the `test-program.asm` and `test-program.dbg` files in this directory for a working example.

The test program:
- Loads a value into the accumulator
- Stores it to zero page
- Loops 5 times using X register
- Adds 2 to the stored value

## Features

### Source Breakpoints ✅
Set breakpoints in your `.asm` files by clicking in the gutter. The debugger will resolve the line number to the correct address.

### Stack Frames with Source ✅
The call stack shows the source file name and line number instead of just the address.

### Verified Breakpoints
- **Green dot**: Breakpoint successfully resolved to an address
- **Gray dot**: Breakpoint could not be resolved (line has no code)

## Backward Compatibility

The debugger still works without debug symbols:
- **Without `.dbg`**: Use disassembly view and instruction breakpoints (original MVP behavior)
- **With `.dbg`**: Full source-level debugging

## Binary File Formats

The debugger supports two binary formats:

### .prg Files (Commodore Format)
Standard Commodore 64 format with a 2-byte load address header (little-endian):
```
[Load Address Low] [Load Address High] [Program Data...]
```

Example:
```bash
ca65 test-program.asm -o test-program.o --debug-info
ld65 -C example.cfg -o test-program.prg test-program.o
```

### .bin Files (Raw Binary)
Raw binary data without a load address header. The load address is obtained from:
1. The `loadAddress` parameter in launch.json (if specified)
2. The CODE segment start address in the `.dbg` file (if provided)

Example launch.json for .bin files:
```json
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Debug .bin with .dbg",
  "program": "${workspaceFolder}/test-program.bin",
  "dbgFile": "${workspaceFolder}/test-program.dbg",
  "stopOnEntry": true
}
```

Or with explicit load address:
```json
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Debug .bin with explicit address",
  "program": "${workspaceFolder}/test-program.bin",
  "loadAddress": 49152,  // $c000
  "stopOnEntry": true
}
```

To create a .bin file from a .prg file:
```bash
# Remove the 2-byte load address header
tail -c +3 test-program.prg > test-program.bin
```

## Limitations

1. **Single-file support**: Currently optimized for single `.asm` files
2. **No macro expansion**: Shows original source, not macro-expanded code
3. **Address range**: One source line maps to one address (first byte of instruction)

## Troubleshooting

### "Could not resolve line X to address"
- The line has no executable code (comment, blank, label-only)
- Try setting the breakpoint on the next instruction line

### "Warning: Debug file not found"
- Check that the `dbgFile` path in launch.json is correct
- Ensure you assembled with `--debug-info` flag
- Verify the `.dbg` file was created by ld65

### Breakpoints not working
- Make sure you're setting breakpoints in the same source file referenced in the `.dbg` file
- The file name in the `.dbg` must match your actual source file name

## Debug File Format

The ca65 `.dbg` format is a text file with records:
- `file`: Source file definitions
- `seg`: Memory segment information
- `span`: Code spans within segments
- `line`: Line number to address mappings
- `sym`: Symbol definitions

Example:
```
version major=2,minor=0
file    id=0,name="test-program.asm",size=498,mtime=0x697C9BBD,mod=0
seg     id=0,name="CODE",start=0x00c000,size=0x00000F,addrsize=absolute,type=rw
span    id=0,seg=0,start=0,size=2,type=1
line    id=0,file=0,line=9,span=0
sym     id=0,name="start",addrsize=absolute,size=1,val=0xc000,seg=0,type=lab
```

## Next Steps

Future enhancements could include:
- Multi-file project support
- Symbol name resolution in variables view
- Hover tooltips showing symbol values
- Support for other assembler debug formats
