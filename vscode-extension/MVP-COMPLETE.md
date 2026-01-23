# VSCode 6502 Debugger - MVP Implementation Complete! 🎉

## What Was Built

I've successfully implemented a minimal viable VSCode debugger for 6502 machine code programs running in the dotnet-6502 emulator.

### Components Created

1. **Debug Adapter (C# Console Application)**
   - Location: `src/apps/Highbyte.DotNet6502.DebugAdapter/`
   - Implements Debug Adapter Protocol (DAP) via StreamJsonRpc
   - Integrates with existing dotnet-6502 CPU emulator
   - Files:
     - `Program.cs` - Entry point
     - `DebugAdapter6502.cs` - Main DAP implementation (350+ lines)
     - `Protocol/DapTypes.cs` - DAP message types

2. **VSCode Extension (TypeScript)**
   - Location: `vscode-extension/`
   - Minimal extension that launches the debug adapter
   - Files:
     - `src/extension.ts` - Extension activation and adapter locator
     - `src/debugAdapter.ts` - Debug session handler (minimal)
     - `package.json` - Extension manifest with debug configuration
     - `tsconfig.json` - TypeScript configuration

3. **Documentation**
   - `vscode-extension/README.md` - User guide
   - `vscode-extension/TESTING.md` - Testing instructions
   - `vscode-extension/DEVELOPMENT.md` - Developer setup
   - `vscode-extension/IMPLEMENTATION.md` - Technical documentation

## Features Implemented ✅

### Debugging Capabilities
- ✅ **Launch .prg files** from VSCode with configurable load address
- ✅ **Address-based breakpoints** - Set breakpoints at memory addresses
- ✅ **Step execution** - Step, step in, step out (single instruction)
- ✅ **Continue/Pause** - Run until breakpoint or BRK instruction
- ✅ **Register inspection** - View PC, SP, A, X, Y in Variables panel
- ✅ **Flag inspection** - View N, V, B, D, I, Z, C flags
- ✅ **Disassembly view** - See current instruction in Call Stack

### Technical Highlights
- Uses existing `BinaryLoader`, `CPU`, `Memory`, `OutputGen` from dotnet-6502
- Proper async/await patterns for DAP handlers
- Type-safe RPC methods with JsonRpcMethod attributes
- Clean separation: UI (VSCode) → Protocol (DAP) → Logic (Emulator)

## How to Use

### Quick Start
```bash
# 1. Build the debug adapter
dotnet build src/apps/Highbyte.DotNet6502.DebugAdapter

# 2. Install extension dependencies
cd vscode-extension
npm install
npm run compile

# 3. Open in VSCode Extension Development Host
# Press F5 in VSCode (from repo root)

# 4. In the new window, create launch.json:
{
  "type": "6502",
  "request": "launch",
  "name": "Debug 6502 Program",
  "program": "${workspaceFolder}/program.prg",
  "stopOnEntry": true
}

# 5. Start debugging with F5!
```

### Setting Breakpoints
- Click in the gutter at a line number
- **Important**: Line number = memory address
- Example: Line 1536 = address 0x0600 ($0600)

### Viewing State
- **Variables Panel**: Shows Registers (PC, SP, A, X, Y) and Flags (N, V, B, D, I, Z, C)
- **Call Stack**: Shows disassembled instruction at current PC
- **Debug Console**: Shows output and errors

## Architecture

```
┌───────────────────────────────┐
│  VSCode Extension (TypeScript) │
│  Locates and launches adapter  │
└───────────┬───────────────────┘
            │ DAP over stdio
┌───────────▼───────────────────┐
│  Debug Adapter (C#)            │
│  - Handles DAP requests        │
│  - Controls CPU execution      │
│  - Manages breakpoints         │
└───────────┬───────────────────┘
            │ Direct API calls
┌───────────▼───────────────────┐
│  Highbyte.DotNet6502 Library   │
│  CPU, Memory, BinaryLoader     │
└───────────────────────────────┘
```

## Known Limitations (By Design for MVP)

1. **No source-level debugging** - Must use memory addresses, not .asm files
2. **Line number = address** - Line 1536 means address 0x0600
3. **No memory inspection** - Only registers visible (no memory view)
4. **Address-only breakpoints** - No conditional breakpoints
5. **Single-step semantics** - No "step over JSR" logic yet

## What's Next (Post-MVP Ideas)

### High Priority
- [ ] Source-level debugging (.asm file support)
- [ ] Memory view/editing
- [ ] Symbol file support (.lst, .sym)
- [ ] Conditional breakpoints

### Medium Priority
- [ ] Watch expressions
- [ ] Step over JSR (call stack tracking)
- [ ] Multiple disassembly lines
- [ ] Hover to inspect memory

### Low Priority
- [ ] Data breakpoints (memory watch)
- [ ] Reverse debugging
- [ ] Hot reload
- [ ] Integrated assembler

## Testing Status

### Manual Testing Needed
- [ ] Load and launch a .prg file
- [ ] Set breakpoint and verify it's hit
- [ ] Step through instructions
- [ ] Continue execution
- [ ] Inspect registers and flags
- [ ] View disassembly in call stack
- [ ] Disconnect/terminate

### Test with Sample Programs
You can test with:
1. `vscode-extension/test-program.asm` (needs assembling)
2. Sample .prg files from `samples/` directory in repo
3. Any Commodore 64 .prg file

## Commit Information

- **Branch**: `feature/vscode-debugger`
- **Commit**: "Add VSCode debugger MVP for 6502 machine code"
- **Files Changed**: 17 files, 1261 insertions
- **Status**: Ready for testing and review

## Key Files to Review

1. **Debug Adapter Core Logic**:
   - `src/apps/Highbyte.DotNet6502.DebugAdapter/DebugAdapter6502.cs`

2. **VSCode Integration**:
   - `vscode-extension/src/extension.ts`
   - `vscode-extension/package.json`

3. **Documentation**:
   - `vscode-extension/TESTING.md` - Start here for testing
   - `vscode-extension/IMPLEMENTATION.md` - Technical deep dive

## Success Criteria ✅

All MVP goals achieved:
- ✅ Simple DAP server with launch support
- ✅ Address-only breakpoints
- ✅ Continue/pause/step operations
- ✅ Register inspection
- ✅ Show disassembly at current PC

## Dependencies Added

### C# Project
- `StreamJsonRpc` 2.19.27 (added to Directory.Packages.props)

### TypeScript Extension
- `@vscode/debugadapter` ^1.65.0
- `@vscode/debugprotocol` ^1.65.0
- `@types/vscode` ^1.80.0

## Build Status

✅ Debug adapter builds successfully with only warnings:
- Warning: MessagePack vulnerability (from StreamJsonRpc dependency)
- Warning: Unused field `_stepRequested` (intentional for future use)

## Ready for Testing! 🚀

The implementation is complete and committed. To start testing:

1. Open the project in VSCode
2. Follow the Quick Start guide above
3. Try debugging a .prg file
4. Report any issues or suggestions

---

**Total Implementation Time**: ~1 session
**Lines of Code**: ~1300 (C# + TypeScript + docs)
**Files Created**: 17
**MVP Status**: ✅ COMPLETE
