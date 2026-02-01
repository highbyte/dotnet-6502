# Task Provider

The VS Code extension provides an automatic task called **"ca65: build current file (C64)"** that builds the currently open `.asm` file using C64-specific configuration.

## How It Works

The Task Provider automatically:
1. Provides a single task "ca65: build current file (C64)"
2. Uses VS Code variables (`${file}`, `${fileBasename}`, etc.) to build the active file
3. Makes it available in **Terminal > Run Task** menu and as `preLaunchTask`
4. Works with any .asm file you open - no configuration needed
5. Auto-detects the built .prg and .dbg files - no need to specify them in launch.json
6. Uses `-C c64-asm.cfg` for C64-compatible builds

## Task Naming

The task is always named: **ca65: build current file (C64)**

The "(C64)" suffix indicates it uses C64-specific configuration (`-C c64-asm.cfg`).

## Load Address

**Important**: You must specify the load address in your .asm file:

```asm
.org $c000    ; Set load address to $c000

start:
    lda #$01
    sta $d020   ; Border color
    rts
```

Without `.org`, the cl65 default will be used, which may not be appropriate for your program.

### How to Use the Task

**Method 1: As preLaunchTask** (Recommended)

First, add `.org` directive to your .asm file:
```asm
.org $c000

start:
    lda #$01
    ; your code...
```

Then in launch.json:
```json
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Debug Current File",
  "preLaunchTask": "ca65: build current file (C64)",
  "stopOnEntry": true
}
```

Open the `.asm` file you want to debug and press F5 - it will be built automatically. The extension auto-detects the built `.prg` and `.dbg` files.

**Method 2: Manual Build**
- Open the .asm file you want to build
- Go to **Terminal → Run Task**
- Select "ca65: build current file (C64)"

## Default Build Command

The task runs:
```bash
cl65 -g ${fileBasename} -o ${fileBasenameNoExtension}.prg \
  -C c64-asm.cfg \
  -Wl "-Ln,${fileBasenameNoExtension}.lbl" \
  -Wl "--dbgfile,${fileBasenameNoExtension}.dbg" \
  -Wl "-m,${fileBasenameNoExtension}.map" && echo "Build complete"
```

Where:
- `${fileBasename}` = Currently open file (e.g., "program.asm")
- `${fileBasenameNoExtension}` = Filename without extension (e.g., "program")
- `${fileDirname}` = Directory of the file (used as working directory)
- `-C c64-asm.cfg` = Use C64 assembly configuration (not full C runtime)

**Note**: No `--start-addr` parameter. The load address must be specified in your source with `.org`.

This generates:
- `.prg` - Program file (with 2-byte load address header)
- `.lbl` - Label file
- `.dbg` - Debug symbols (ca65 format)
- `.map` - Memory map

## Customizing Build Parameters

The default task uses minimal arguments. If you need specific build settings (config file, start address, optimization, etc.), create a custom task in `tasks.json`:

### Custom Task Example

Create `.vscode/tasks.json`:
- `"-D", "<name>=<value>"` - Define preprocessor symbol
- `"-O"` - Enable optimizations
### Option 2: Create Custom Task in tasks.json

For reusable build configurations:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build with custom config",
      "type": "shell",
      "command": "cl65",
      "args": [
        "-g",
        "${file}",
        "-o",
        "${fileBasenameNoExtension}.prg",
        "-C",
        "atari.cfg",
        "-Wl", "-Ln,${fileBasenameNoExtension}.lbl",
        "-Wl", "--dbgfile,${fileBasenameNoExtension}.dbg",
        "-Wl", "-m,${fileBasenameNoExtension}.map"
      ],
      "problemMatcher": "$ca65",
      "group": "build"
    }
  ]
}
```

Then use it in launch.json:
```json
{
  "preLaunchTask": "build with custom config",
  "program": "${workspaceFolder}/program.prg",
  "dbgFile": "${workspaceFolder}/program.dbg",
  "stopOnEntry": true
}
```

**Benefits**:
- Custom task names
- Can run manually (Cmd/Ctrl+Shift+B)
- Shared across all launch configs
- Full control over build arguments

### Common Build Arguments

- `"-C", "c64-asm.cfg"` - Use C64 assembly configuration (default in provided task)
- `"-C", "atari.cfg"` - Use Atari configuration
- `"-D", "DEBUG=1"` - Define preprocessor symbol
- `"-O"` - Enable optimizations
- `"--cpu", "65C02"` - Target specific CPU

**Note**: The load address should be specified in your source code using `.org`, not via `--start-addr`.

### Manual Build Process

If you want complete control over your build process (e.g., using Makefiles, shell scripts, or manual commands), simply omit `preLaunchTask`:

**launch.json**:
```json
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Debug pre-built program",
  "program": "${workspaceFolder}/program.prg",
  "dbgFile": "${workspaceFolder}/program.dbg",
  "stopOnEntry": true
}
```

**How it works**:
- Build your program manually before debugging (run `cl65` yourself, use a Makefile, etc.)
- The debugger just loads the pre-built `.prg` and `.dbg` files
- No automatic building - you're in complete control

**Good for**: 
- Complex build processes
- Multi-file projects with custom build scripts
- Integration with external build systems
- When you prefer manual control

**Optional**: Create a custom build task in `.vscode/tasks.json` that you can run manually:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "Build with Makefile",
      "type": "shell",
      "command": "make",
      "args": ["all"],
      "problemMatcher": "$ca65",
      "group": {
        "kind": "build",
        "isDefault": true
      }
    },
    {
      "label": "Clean build",
      "type": "shell",
      "command": "make",
      "args": ["clean"],
      "problemMatcher": []
    }
  ]
}
```

Then build manually with:
- **Terminal → Run Build Task** (Cmd/Ctrl+Shift+B)
- **Terminal → Run Task** → "Build with Makefile"
- Or run `make` directly in the terminal

## Using with launch.json

Reference the provided task or a custom task as a pre-launch task:

**Using the provided task** (auto-detects program/dbgFile):
```json
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Debug current file",
  "preLaunchTask": "ca65: build current file (C64)",
  "stopOnEntry": true
}
```

**Using a custom task**:
```json
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Debug with custom config",
  "preLaunchTask": "build with custom config",
  "program": "${workspaceFolder}/program.prg",
  "dbgFile": "${workspaceFolder}/program.dbg",
  "stopOnEntry": true
}
```

## No Configuration Required

Unlike manual `tasks.json` setup, the Task Provider requires **zero configuration**. Just open an `.asm` file, and the build task is available.

## Task Behavior

The task:
- Builds whichever .asm file is currently active in the editor
- Uses VS Code variables (`${file}`, `${fileBasename}`, etc.)
- Outputs to the same directory as the source file
- Auto-detected by the debug adapter when used as `preLaunchTask`

## Problem Matcher

Tasks include the `$ca65` problem matcher which parses compiler errors/warnings and displays them in:
- **Problems** panel (`Cmd/Ctrl+Shift+M`)
- Inline error squiggles in the editor

Error format: `filename(line): Error: message`
