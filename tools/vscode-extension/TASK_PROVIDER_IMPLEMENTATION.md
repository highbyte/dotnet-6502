# Task Provider Implementation Summary

## What Was Implemented

A VS Code Task Provider that automatically detects `.asm` files in the workspace and generates build tasks without requiring manual `tasks.json` configuration.

## Files Created/Modified

### New Files
1. **src/ca65TaskProvider.ts** - Task Provider implementation
   - Implements `vscode.TaskProvider` interface
   - `provideTasks()`: Scans workspace for `*.asm` files
   - `resolveTask()`: Resolves task definitions
   - `createBuildTask()`: Generates cl65 build tasks for each .asm file
   - `clear()`: Clears task cache when files change

2. **TASK_PROVIDER.md** - Documentation for Task Provider feature

### Modified Files
1. **src/extension.ts**
   - Imported `Ca65TaskProvider`
   - Registered task provider in `activate()` function
   - Added file watcher to refresh tasks when .asm files are added/deleted
   - Disposed task provider in `deactivate()`

2. **package.json**
   - Added `taskDefinitions` contribution point for "ca65" type
   - Added `problemMatchers` contribution with "$ca65" matcher
   - Pattern: `^(.*)\\((\\d+)\\):\\s+(Error|Warning):\\s+(.*)$`

3. **README.md**
   - Updated features list to include Task Provider
   - Added "Configuration" section with automatic build tasks documentation
   - Updated launch.json examples to show preLaunchTask usage
   - Removed "Limitations" about no source-level debugging

4. **.vscode/launch.json** (vscode-extension-test)
   - Changed `preLaunchTask` from manual "Build test-program" to auto-generated "ca65: build test-program.asm"

5. **Deleted: .vscode/tasks.json** (vscode-extension-test)
   - No longer needed, Task Provider handles this automatically

## How It Works

### Task Discovery
1. User opens workspace with `.asm` files
2. Extension activates and registers Ca65TaskProvider
3. Provider scans workspace for `**/*.asm` files
4. Creates a task for each file found

### Task Naming Convention
- Format: `ca65: build <filename>.asm`
- Example: `ca65: build test-program.asm`

### Generated Build Command
```bash
cl65 -g <file>.asm -o <base>.prg -C c64-asm.cfg --start-addr 0x0600 \
  -Wl "-Ln,<base>.lbl" \
  -Wl "--dbgfile,<base>.dbg" \
  -Wl "-m,<base>.map"
```

Outputs: `.prg`, `.lbl`, `.dbg`, `.map`

### Problem Matcher Integration
- Tasks include `$ca65` problem matcher
- Parses error format: `filename(line): Error: message`
- Shows errors in Problems panel
- Displays inline error squiggles in editor

### Usage in launch.json
```json
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Debug test-program",
  "preLaunchTask": "ca65: build test-program.asm",
  "program": "${workspaceFolder}/test-program.prg",
  "dbgFile": "${workspaceFolder}/test-program.dbg",
  "stopOnEntry": true
}
```

### Task Refresh
Tasks are automatically refreshed when:
- .asm files are created (file watcher)
- .asm files are deleted (file watcher)
- VS Code window is reloaded

## Benefits

### Before (Manual Configuration)
Users had to:
1. Create `.vscode/tasks.json`
2. Define task with all command arguments
3. Configure problem matcher
4. Set up presentation options
5. Reference task name exactly in launch.json

### After (Task Provider)
Users just:
1. Add `.asm` files to workspace
2. Reference auto-generated task: `"preLaunchTask": "ca65: build filename.asm"`

**Zero configuration required!**

## Testing

1. Compile extension: `npm run compile` ✓
2. No TypeScript errors ✓
3. Extension compiles successfully ✓
4. launch.json updated to use auto-generated task ✓
5. Manual tasks.json removed ✓

## Next Steps for User

1. Press F5 in vscode-extension folder to launch Extension Development Host
2. Open vscode-extension-test folder in the dev host
3. Check **Terminal → Run Task** menu - should see:
   - `ca65: build test-program.asm`
   - `ca65: build test-program2.asm`
4. Press F5 to debug - preLaunchTask should run automatically
5. Verify build errors appear in Problems panel

## VS Code Extension Patterns Used

- **Task Provider API**: Standard VS Code pattern (like npm, TypeScript, Python extensions)
- **Problem Matchers**: Parse compiler output for Problems panel integration
- **File System Watcher**: Auto-refresh tasks when workspace changes
- **Task Definitions**: Declare custom task types in package.json
- **Shell Execution**: Run cl65 commands with proper working directory

## References

- [VS Code Task Provider API](https://code.visualstudio.com/api/extension-guides/task-provider)
- [Problem Matchers](https://code.visualstudio.com/docs/editor/tasks#_defining-a-problem-matcher)
- [Extension Activation](https://code.visualstudio.com/api/references/activation-events)
