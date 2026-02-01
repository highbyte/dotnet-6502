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
      "command": "cl65",
      "args": [
        "-g",
        "test-program.asm",
        "-o",
        "test-program.prg",
        "-C",
        "c64-asm.cfg",
        "--start-addr",
        "0xc000",
        "-Wl", "-Ln,test-program.lbl",
        "-Wl", "--dbgfile,test-program.dbg",
        "-Wl", "-m,test-program.map"
      ],
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
      "command": "cl65",
      "args": [..., "--start-addr", "0xc000"]
    },
    {
      "label": "Build loader.asm (C64)",
      "command": "cl65",
      "args": [..., "--start-addr", "0x0801"]
    },
    {
      "label": "Build music.asm (C64)",
      "command": "cl65",
      "args": [..., "--start-addr", "0x1000"]
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
- Add optimization: Add `"-O"` to args
- Add defines: Add `"-D", "DEBUG=1"` to args
- Change CPU target: Add `"--cpu", "65C02"` to args
- Modify any compiler flags

Example customized task:

```json
{
  "label": "Build game.asm (C64 Optimized)",
  "type": "shell",
  "command": "cl65",
  "args": [
    "-g",
    "-O",
    "game.asm",
    "-o",
    "game.prg",
    "-C",
    "c64-asm.cfg",
    "--start-addr",
    "0xc000",
    "--cpu",
    "6502",
    "-D", "RELEASE=1",
    "-Wl", "-Ln,game.lbl",
    "-Wl", "--dbgfile,game.dbg",
    "-Wl", "-m,game.map"
  ],
  "problemMatcher": "$ca65",
  "group": "build"
}
```

## Updating Existing Tasks

If you run "Generate C64 Build Task" on a file that already has a task:
- The extension will ask if you want to overwrite it
- Choose "Yes" to update with new settings
- Choose "No" to keep the existing task

## Comparison with TaskProvider

The extension also provides a generic TaskProvider task:
- **Name**: "ca65: build current file (C64)"
- **Purpose**: Quick builds without configuration
- **Limitation**: No `--start-addr` parameter (uses c64-asm.cfg default of 0x0801)

Use the **TaskProvider task** for:
- Quick testing
- Files that work with default 0x0801 load address
- Temporary builds

Use **Generate Build Task** for:
- Production code
- Custom load addresses (most C64 programs use 0xc000 or higher)
- Per-file build customization
- Reliable debugging with correct addresses

## Troubleshooting

**Q: The .prg file has wrong load address (0x0801 instead of 0xc000)**  
A: Make sure you're using the generated task, not the generic TaskProvider task. Check your `preLaunchTask` name in launch.json matches the task label in tasks.json.

**Q: Can I change the start address after generating the task?**  
A: Yes! Just edit the `--start-addr` value in `.vscode/tasks.json` directly.

**Q: Do I need to regenerate tasks if I rename my .asm file?**  
A: Yes, generate a new task for the renamed file, or manually update the file references in tasks.json.

**Q: Can I use this with other assemblers (KickAssembler, ACME, etc.)?**  
A: The current implementation is specific to cc65/ca65. For other assemblers, manually create tasks in tasks.json with the appropriate commands.
