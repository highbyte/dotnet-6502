# Folder Structure Explanation

This repo has two separate folders for the debugger:

## 1. `tools/vscode-extension/` - Extension Source Code

**Purpose**: Developing the VSCode extension itself

**Contents**:
- Extension source code (TypeScript)
- `.vscode/launch.json` - Launches **Extension Development Host**
- `.vscode/tasks.json` - Compiles the extension
- Test program source for reference

**How to use**:
1. Open this folder in VSCode
2. Press F5 → Extension Development Host window opens
3. This is the "extension development" window

## 2. `tools/vscode-extension-test/` - Test Workspace

**Purpose**: Testing/using the 6502 debugger

**Contents**:
- `test-program.prg` - 6502 program to debug
- `test-program.asm` - Source code
- `build-test-program.ps1` - Build script
- `.vscode/launch.json` - Debugs **6502 program** (not the extension)

**How to use**:
1. Open this folder in the **Extension Development Host** window
2. Press F5 → Debugs test-program.prg with the 6502 debugger
3. This is the "using the debugger" window

## Summary

```
┌─────────────────────────────────────────┐
│ Main VSCode Window                      │
│ Folder: vscode-extension/               │
│ Purpose: Extension development          │
│                                         │
│ Press F5 → Launches ↓                   │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│ Extension Development Host Window       │
│ Folder: vscode-extension-test/          │
│ Purpose: Test 6502 debugger             │
│                                         │
│ Press F5 → Debugs 6502 program          │
└─────────────────────────────────────────┘
```

## Why Two Folders?

- **Separation of concerns**: Extension code vs. programs to debug
- **Avoid confusion**: Each folder has its own `.vscode/launch.json` for different purposes
- **Clean testing**: Test workspace can have multiple .prg files without cluttering extension source

## For End Users

When the extension is published, users only need:
- The `tools/vscode-extension-test/` pattern (any folder with .prg files)
- They don't see `tools/vscode-extension/` - that's just for development
