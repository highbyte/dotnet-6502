# Quick Start Guide - Task Provider

## For End Users

### 1. Create an Assembly File
Create a file `program.asm` in your workspace:
```assembly
; Simple 6502 program
.org $0600

start:
    LDA #$01    ; Load 1 into A
    STA $0200   ; Store at $0200
    LDA #$05    ; Load 5 into A
    STA $0201   ; Store at $0201
    LDA #$08    ; Load 8 into A
    STA $0202   ; Store at $0202
    RTS         ; Return
```

### 2. Create launch.json
No tasks.json needed! Just create `.vscode/launch.json`:
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "type": "dotnet6502",
      "request": "launch",
      "name": "Debug program",
      "preLaunchTask": "ca65: build current file (C64)",
      "stopOnEntry": true
    }
  ]
}
```

**Note**: No need to specify `program` or `dbgFile` - they are auto-detected from the built files!

### 3. Debug
Press **F5** - the extension will:
1. Auto-build `program.asm` → `program.prg` + `program.dbg`
2. Load the program into the emulator
3. Stop at the first instruction
4. Allow source-level debugging with breakpoints in your .asm file

### 4. View Available Task
**Terminal → Run Task** to see the auto-provided task:
- `ca65: build current file (C64)`

This task builds whichever .asm file is currently open in the editor.

## For Extension Developers

### Testing the Extension

1. **Open extension folder**:
   ```bash
   cd tools/vscode-extension
   code .
   ```

2. **Compile extension**:
   ```bash
   npm run compile
   ```

3. **Launch Extension Development Host**:
   Press **F5** in VS Code

4. **In the dev host window, open test workspace**:
   ```bash
   # File → Open Folder → tools/vscode-extension-test
   ```

5. **Verify task is available**:
   - **Terminal → Run Task**
   - Should see: `ca65: build current file (C64)`

6. **Start debugging**:
   - Press **F5**
   - preLaunchTask should auto-build
   - Debugger should launch and stop at entry point

### Debug Output

Check the Extension Host console for logs:
```
[6502 Debug] Extension activating...
[6502 Debug] Extension activated successfully
```

## How Tasks Are Generated

The Task Provider creates a single task that builds the currently open file:

**Task name**: `ca65: build current file (C64)`

**Command**:
```bash
cl65 -g ${fileBasename} -o ${fileBasenameNoExtension}.prg -C c64-asm.cfg \
  -Wl "-Ln,${fileBasenameNoExtension}.lbl" \
  -Wl "--dbgfile,${fileBasenameNoExtension}.dbg" \
  -Wl "-m,${fileBasenameNoExtension}.map" && echo "Build complete"
```

**Working directory**: Directory containing the .asm file

**Notes**: 
- Uses `-C c64-asm.cfg` for C64-compatible assembly
- No `--start-addr` - you must specify load address with `.org` in your source
  -Wl "-Ln,<name>.lbl" \
  -Wl "--dbgfile,<name>.dbg" \
  -Wl "-m,<name>.map"
```

**Working directory**: Directory containing the .asm file

**Problem matcher**: `$ca65` (parses compiler errors)

## Troubleshooting

### Tasks Not Appearing
- Ensure workspace contains `.asm` files
## Troubleshooting

### Task Not Appearing
- Ensure extension is activated: look for "[6502 Debug] Extension activated" in console
- Reload window: **Cmd/Ctrl+Shift+P** → "Reload Window"
- Task is always available - doesn't require .asm files to exist

### Build Errors Not Showing
- Errors should appear in **Problems** panel (Cmd/Ctrl+Shift+M)
- Check terminal output for full error messages
- Verify cl65 is installed: `which cl65` in terminal

### preLaunchTask Fails
- Verify task name exactly matches: `"ca65: build current file (C64)"`
- Ensure an .asm file is open in the editor when debugging
- Check cl65 is in PATH
- Verify your .asm file has `.org` directive to set load address
- Try running task manually: **Terminal → Run Task**

## Requirements

- **cc65 toolchain**: Install from [cc65.github.io](https://cc65.github.io/)
  ```bash
  # macOS
  brew install cc65
  
  # Linux
  apt-get install cc65
  
  # Windows
  # Download from cc65.github.io
  ```

- **.NET 10.0**: For the debug adapter
  ```bash
  dotnet --version
  ```

## Key Benefits

✅ **Zero configuration** - No tasks.json needed  
✅ **Single universal task** - Works with any .asm file you open  
✅ **Auto-detection** - program/dbgFile found automatically when using preLaunchTask  
✅ **Error integration** - Compiler errors in Problems panel  
✅ **One-click debugging** - Just press F5
