# Generate Build Task Feature

## Overview

The extension provides a command to generate customized build tasks for individual .asm files. This solves the problem of needing different `--start-addr` values for different files.

## How to Use

### Step 1: Generate the Task

1. **Right-click** your `.asm` file in the Explorer
2. Select **"Generate C64 Build Task (ca65)"**
3. Enter the start/load address when prompted (e.g., `0xc000`)
4. The extension creates a task in `.vscode/tasks.json`

### Step 2: Create Launch Configuration (Optional)

After generating the task, you'll see a prompt with options:
- **"Open tasks.json"** - View the generated task
- **"Create Launch Config"** - Automatically create a matching launch configuration

If you choose "Create Launch Config", the extension will:
- Create/update `.vscode/launch.json`
- Add a debug configuration that uses your new build task
- Ready to debug with F5!

### Step 3: Debug

Press **F5** to start debugging. The task will:
1. Build your .asm file with the specified start address
2. Generate .prg, .dbg, .lbl, and .map files
3. Launch the debugger automatically

## What Gets Generated

### tasks.json

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "Build test-program.asm (C64)",
      "type": "shell",
      "command": "ca65 -g test-program.asm -o test-program.o && ld65 test-program.o -o test-program.prg -C c64-asm.cfg --start-addr 0xc000 -Ln test-program.lbl --dbgfile test-program.dbg -m test-program.map",
      "options": {
        "cwd": "${workspaceFolder}"
      },
      "problemMatcher": "$ca65",
      "group": {
        "kind": "build",
        "isDefault": false
      }
    }
  ]
}
```

### launch.json

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "type": "dotnet6502",
      "request": "launch",
      "name": "Debug test-program.asm",
      "preLaunchTask": "Build test-program.asm (C64)",
      "stopOnEntry": true,
      "stopOnBRK": true
    }
  ]
}
```

## Benefits

✅ **Per-file configuration** - Each .asm file can have its own build task with specific settings  
✅ **No custom syntax** - Uses standard VSCode tasks.json format  
✅ **Easy customization** - Edit tasks.json to change compiler flags, config files, etc.  
✅ **Works with preLaunchTask** - Seamlessly integrates with launch configurations  
✅ **Correct load addresses** - Generates proper .prg headers and .dbg files  

## Multiple Files

Generate a task for each .asm file that needs a different configuration:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "Build game.asm (C64)",
      "command": "ca65 -g game.asm -o game.o && ld65 game.o -o game.prg -C c64-asm.cfg --start-addr 0xc000 -Ln game.lbl --dbgfile game.dbg -m game.map"
    },
    {
      "label": "Build loader.asm (C64)",
      "command": "ca65 -g loader.asm -o loader.o && ld65 loader.o -o loader.prg -C c64-asm.cfg --start-addr 0x0801 -Ln loader.lbl --dbgfile loader.dbg -m loader.map"
    },
    {
      "label": "Build music.asm (C64)",
      "command": "ca65 -g music.asm -o music.o && ld65 music.o -o music.prg -C c64-asm.cfg --start-addr 0x1000 -Ln music.lbl --dbgfile music.dbg -m music.map"
    }
  ]
}
```

Then create separate launch configurations for each:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Debug game.asm",
      "preLaunchTask": "Build game.asm (C64)"
    },
    {
      "name": "Debug loader.asm",
      "preLaunchTask": "Build loader.asm (C64)"
    },
    {
      "name": "Debug music.asm",
      "preLaunchTask": "Build music.asm (C64)"
    }
  ]
}
```

## Customizing Tasks

After generation, you can edit the task in `tasks.json` to:

- Change the config file: `-C atari.cfg` instead of `-C c64-asm.cfg`
- Add defines: Add `-D RELEASE=1` to the ca65 command
- Change CPU target: Add `--cpu 65C02` to the ca65 command
- Modify any assembler or linker flags

Example customized task:

```json
{
  "label": "Build game.asm (C64 Optimized)",
  "type": "shell",
  "command": "ca65 -g --cpu 6502 -D RELEASE=1 game.asm -o game.o && ld65 game.o -o game.prg -C c64-asm.cfg --start-addr 0xc000 -Ln game.lbl --dbgfile game.dbg -m game.map",
  "problemMatcher": "$ca65",
  "group": "build"
}
```

## Updating Existing Tasks

If you run "Generate C64 Build Task" on a file that already has a task:
- The extension will ask if you want to overwrite it
- Choose "Yes" to update with new settings
- Choose "No" to keep the existing task

## Why Generate Tasks?

Generated tasks provide:
- **Custom load addresses** - Essential for C64 programs (most use 0xc000 or higher)
- **Per-file configuration** - Each .asm file can have different settings
- **Full customization** - Edit tasks.json to add compiler flags, optimization, etc.
- **Standard VSCode format** - No proprietary configuration or magic
- **Reliable debugging** - Correct .prg headers and .dbg files with proper addresses

## Troubleshooting

**Q: The .prg file has wrong load address (0x0801 instead of 0xc000)**  
A: Make sure you specified the correct `--start-addr` when generating the task. You can edit the value in `.vscode/tasks.json` directly.

**Q: Can I change the start address after generating the task?**  
A: Yes! Just edit the `--start-addr` value in `.vscode/tasks.json` directly.

**Q: Do I need to regenerate tasks if I rename my .asm file?**  
A: Yes, generate a new task for the renamed file, or manually update the file references in tasks.json.

**Q: Can I use this with other assemblers (KickAssembler, ACME, etc.)?**  
A: The current implementation is specific to cc65/ca65. For other assemblers, manually create tasks in tasks.json with the appropriate commands.
