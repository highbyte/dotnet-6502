# VSCode 6502 Debugger - MVP Implementation

## Summary

This is a minimal viable product (MVP) implementation of a VSCode debugger for 6502 machine code programs using the dotnet-6502 emulator.

## What's Implemented

### Debug Adapter (C#)
- **Project**: `src/apps/Highbyte.DotNet6502.DebugAdapter`
- **Protocol**: Debug Adapter Protocol (DAP) via StreamJsonRpc
- **Features**:
  - Launch 6502 programs from .prg files
  - Address-based breakpoints
  - Step/continue/pause execution
  - Register inspection (PC, SP, A, X, Y)
  - Flag inspection (N, V, B, D, I, Z, C)
  - Disassembly at current PC

### VSCode Extension (TypeScript)
- **Location**: `tools/vscode-extension/`
- **Features**:
  - Debug configuration provider
  - Launches debug adapter executable
  - Standard VSCode debugging UI integration

## Architecture

```
┌─────────────────────────────────────┐
│   VSCode Extension (TypeScript)     │
│  - extension.ts: Activation & setup │
│  - debugAdapter.ts: Session handler │
└─────────────┬───────────────────────┘
              │ DAP Protocol
              │ (stdin/stdout)
┌─────────────▼───────────────────────┐
│  Debug Adapter (C# Console App)     │
│  - DebugAdapter6502.cs: DAP handler │
│  - Protocol/DapTypes.cs: Messages   │
└─────────────┬───────────────────────┘
              │ Direct API calls
┌─────────────▼───────────────────────┐
│    Highbyte.DotNet6502 Library      │
│  - CPU, Memory, BinaryLoader        │
│  - OutputGen (disassembly)          │
└─────────────────────────────────────┘
```

## File Structure

```
dotnet-6502/
├── src/apps/Highbyte.DotNet6502.DebugAdapter/
│   ├── Program.cs                    # Entry point
│   ├── DebugAdapter6502.cs           # Main DAP implementation
│   ├── Protocol/DapTypes.cs          # DAP message types
│   └── Highbyte.DotNet6502.DebugAdapter.csproj
│
└── tools/vscode-extension/
    ├── src/
    │   ├── extension.ts              # Extension activation
    │   └── debugAdapter.ts           # Debug session (minimal)
    ├── package.json                  # Extension manifest
    ├── tsconfig.json                 # TypeScript config
    ├── README.md                     # User documentation
    ├── DEVELOPMENT.md                # Developer guide
    └── TESTING.md                    # Testing instructions
```

## Key Design Decisions

### 1. Address-Only Breakpoints
- **Why**: MVP simplicity, no source file parsing needed
- **How**: Line number in VSCode = memory address
- **Example**: Line 1536 = address 0x0600
- **Future**: Add .asm/.lst file support for source-level debugging

### 2. StreamJsonRpc for DAP
- **Why**: Handles JSON-RPC 2.0 protocol cleanly
- **Alternative**: Custom JSON parsing over stdio
- **Benefit**: Type-safe, attribute-based RPC methods

### 3. Synchronous Execution Model
- **Why**: Simpler MVP implementation
- **How**: Execute instructions on-demand vs. background thread
- **Trade-off**: May need async model for long-running programs

### 4. Minimal VSCode Extension
- **Why**: Most logic in debug adapter (testable without VSCode)
- **How**: Extension just locates and launches the adapter
- **Benefit**: Easy to test adapter independently

## DAP Request/Response Flow

```
1. VSCode → initialize → Adapter
   Adapter → InitializeResponse (capabilities)
   Adapter → initialized event

2. VSCode → launch(program, loadAddress?, stopOnEntry?) → Adapter
   Adapter loads .prg file, creates CPU
   Adapter → stopped event (if stopOnEntry=true)

3. VSCode → setBreakpoints(addresses) → Adapter
   Adapter stores breakpoints

4. VSCode → threads → Adapter
   Adapter → [Thread(1, "6502 CPU")]

5. VSCode → stackTrace(threadId) → Adapter
   Adapter → [StackFrame with disassembly at PC]

6. VSCode → scopes(frameId) → Adapter
   Adapter → [Scope("Registers"), Scope("Flags")]

7. VSCode → variables(scopeRef) → Adapter
   Adapter → [Variable list for registers or flags]

8. VSCode → continue/next/stepIn/stepOut → Adapter
   Adapter executes instruction(s)
   Adapter → stopped event (with reason)

9. VSCode → disconnect → Adapter
   Adapter → terminated event
```

## Current Limitations (By Design for MVP)

1. **No source-level debugging**: Must use memory addresses
2. **No memory inspection**: Only registers visible
3. **No conditional breakpoints**: Address-only
4. **No watch expressions**: Predefined variables only
5. **Single-step only for stepping**: No "step over subroutine" logic
6. **No reverse debugging**: Forward execution only
7. **No hot reload**: Must restart to reload program

## Future Enhancements (Post-MVP)

### High Priority
- [ ] Source-level debugging (.asm file support)
- [ ] Memory view/editing
- [ ] Symbol file support (.lst, .sym files)
- [ ] Conditional breakpoints

### Medium Priority
- [ ] Watch expressions
- [ ] Step over JSR (call stack tracking)
- [ ] Multiple disassembly lines in stack trace
- [ ] Hover to inspect memory addresses

### Low Priority
- [ ] Data breakpoints (memory watch)
- [ ] Reverse debugging
- [ ] Hot reload
- [ ] Integrated assembler

## Testing Strategy

### Unit Testing (Future)
- DAP message serialization/deserialization
- Breakpoint management logic
- CPU state inspection

### Integration Testing
1. **Manual**: Use Extension Development Host
2. **Automated** (Future): VSCode extension test framework

### Test Scenarios
- [x] Load .prg file and stop at entry
- [x] Set breakpoint and hit it
- [x] Step through instructions
- [x] Continue execution
- [x] Inspect registers
- [x] Inspect flags
- [x] View disassembly

## Performance Considerations

- **Current**: Synchronous, single-instruction stepping
- **Bottleneck**: None for MVP (interactive debugging)
- **Future**: May need async execution for continuous running

## Security Considerations

- **File Access**: Only reads user-specified .prg files
- **Execution**: Runs in isolated CPU emulator (no system access)
- **Network**: None (stdio-only communication)

## Maintenance Notes

### Dependencies
- **C# Debug Adapter**: StreamJsonRpc, Highbyte.DotNet6502
- **TypeScript Extension**: @vscode/debugadapter, @vscode/debugprotocol

### Breaking Changes to Avoid
- DAP protocol messages (maintain backward compatibility)
- Debug adapter executable location/name
- Launch configuration schema

### Logging/Debugging
- C# adapter: Console.Error for debug output
- VSCode extension: Use Debug Console and Output panel
- Enable verbose logging with environment variables (future)

## Credits

- **Debug Adapter Protocol**: [Microsoft DAP Specification](https://microsoft.github.io/debug-adapter-protocol/)
- **StreamJsonRpc**: [Microsoft StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc)
