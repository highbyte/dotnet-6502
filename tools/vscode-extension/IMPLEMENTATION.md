# VSCode 6502 Debugger - Implementation

## Summary

A VSCode debugger for 6502 machine code programs using the dotnet-6502 emulator. Supports both a standalone minimal debug adapter (STDIO) and a full emulator host mode (TCP) with source-level debugging via ca65 `.dbg` files.

## What's Implemented

### Debug Adapter Library (C#)
- **Project**: `src/libraries/Highbyte.DotNet6502.DebugAdapter`
- **Protocol**: Debug Adapter Protocol (DAP) via custom JSON-RPC over STDIO or TCP
- **Features**:
  - Launch 6502 programs from `.prg` / `.bin` files
  - Attach to running emulator host via TCP
  - Source-level debugging with ca65 `.dbg` symbol files (including multi-file merge via `dbgFiles`)
  - Address-based breakpoints, source breakpoints, instruction breakpoints, and function breakpoints
  - Conditional breakpoints (expression conditions per address)
  - Logpoints (log message to debug console without stopping, with `{expr}` interpolation)
  - Hit count breakpoints (`= N`, `>= N`, `> N`, `% N` syntax)
  - Step in, step over (JSR detection with temp breakpoint at return address), step out (run until RTS)
  - Continue / pause execution
  - Register inspection and editing (PC, SP, A, X, Y)
  - Flag inspection and editing (N, V, D, I, Z, C)
  - Labels scope (from `.dbg` symbols, shows address + current memory byte)
  - Constants scope (from `.dbg` equates)
  - Full disassembly view (walks entire 64K address space for consistent instruction boundaries)
  - Memory read/write (DAP standard `readMemory`/`writeMemory`)
  - Custom memory dump command (`getMemoryDump`)
  - Watch expressions / hover evaluation (registers, addresses, symbols, immediate operands)
  - Debug Console REPL commands (`dump`, `set`)
  - Goto (set PC to source line or address)
  - Source address map for editor decorations
  - `stopOnBRK` option to break on BRK instructions
  - `skipInterrupts` option to auto-skip hardware IRQ/NMI handlers with no source mapping during stepping
  - Auto-detection of `.prg` and `.dbg` files from `preLaunchTask`

### Debug Adapter Console App (C#)
- **Project**: `src/apps/Highbyte.DotNet6502.DebugAdapter`
- **Role**: Standalone STDIO host for the debug adapter library (minimal mode)

### VSCode Extension (TypeScript)
- **Location**: `tools/vscode-extension/`
- **Features**:
  - Debug configuration provider with two modes: `minimal` (STDIO) and `emulator` (TCP)
  - Attach mode for connecting to already-running emulator hosts
  - Launches and manages emulator host process lifecycle
  - Memory viewer (hex dump via custom DAP request)
  - Inline address decorations (shows `$XXXX` after each mapped source line)
  - Dynamic macro address decoration (updates per stop for macro invocations)
  - Jump to Line / Set PC command (context menu on line numbers)
  - Generate Build Task command (ca65 build task in `tasks.json`)
  - Generate Launch Config commands (minimal, emulator, `.prg` file)
  - Standard VSCode debugging UI integration
  - Support for multiple assembler language IDs (`asm`, `kickass`, `acme`, `ca65`, `dasm`)

## Architecture

```
┌──────────────────────────────────────────────┐
│        VSCode Extension (TypeScript)         │
│  - extension.ts: Activation, config, cmds    │
│  - memoryViewer.ts: Memory hex dump viewer   │
│  - debugAdapter.ts: Session handler (legacy) │
└──────────┬──────────────────┬────────────────┘
           │ DAP (stdin/stdout)│ DAP (TCP)
           │   minimal mode   │  emulator mode
┌──────────▼──────────┐ ┌─────▼────────────────────────┐
│ Debug Adapter        │ │ Emulator Host App            │
│ Console App (STDIO)  │ │ (Avalonia Desktop, etc.)     │
│ - Program.cs         │ │ - TcpDebugAdapterServer      │
└──────────┬───────────┘ │ - AutomatedStartupHandler    │
           │             └─────┬────────────────────────┘
           │                   │
     ┌─────▼───────────────────▼───────────┐
     │  Debug Adapter Library (shared)     │
     │  - DebugAdapterLogic.cs: DAP impl   │
     │  - Ca65DbgParser.cs: Symbol parsing │
     │  - DapProtocol.cs: Message I/O      │
     │  - StdioTransport / TcpTransport    │
     └──────────────┬──────────────────────┘
                    │ Direct API calls
     ┌──────────────▼──────────────────────┐
     │     Highbyte.DotNet6502 Library     │
     │  - CPU, Memory, BinaryLoader        │
     │  - OutputGen (disassembly)          │
     │  - DebuggerBreakpointEvaluator      │
     └─────────────────────────────────────┘
```

## File Structure

```
dotnet-6502/
├── src/libraries/Highbyte.DotNet6502.DebugAdapter/
│   ├── DebugAdapterLogic.cs          # Main DAP implementation (shared)
│   ├── Ca65DbgParser.cs              # ca65 .dbg symbol file parser
│   ├── DapProtocol.cs                # DAP message serialization/I/O
│   ├── IDebugAdapterTransport.cs     # Transport abstraction
│   ├── BaseTransport.cs              # Shared transport logic
│   ├── StdioTransport.cs             # STDIO transport (minimal mode)
│   ├── TcpTransport.cs               # TCP transport (emulator mode)
│   ├── TcpDebugAdapterServer.cs      # TCP server for emulator hosts
│   ├── TcpDebugServerManager.cs      # Server lifecycle management
│   ├── IDebuggableHostApp.cs         # Interface for emulator hosts
│   └── ITcpDebugServerEnvironment.cs # Environment interface
│
├── src/apps/Highbyte.DotNet6502.DebugAdapter/
│   ├── Program.cs                    # STDIO console host entry point
│   └── *.csproj
│
└── tools/vscode-extension/
    ├── src/
    │   ├── extension.ts              # Extension activation, config, commands
    │   ├── memoryViewer.ts           # Memory hex dump content provider
    │   └── debugAdapter.ts           # Debug session (legacy/minimal)
    ├── package.json                  # Extension manifest
    ├── language-configuration.json   # .asm language config
    ├── tsconfig.json                 # TypeScript config
    ├── README.md                     # User documentation
    ├── DEVELOPMENT.md                # Developer guide
    └── TESTING.md                    # Testing instructions
```

## Key Design Decisions

### 1. Source-Level + Address Breakpoints
- **Source breakpoints**: Resolved via ca65 `.dbg` file mappings (source line → address)
- **Instruction breakpoints**: Set in disassembly view by address
- **Function breakpoints**: Enter hex addresses (`$C0D5`, `0xc0d5`) via Breakpoints panel
- **Fallback**: Without `.dbg` file, line number = memory address (legacy mode)

### 2. Dual Transport Architecture
- **Minimal mode (STDIO)**: Standalone debug adapter console app, generic 6502 debugging
- **Emulator mode (TCP)**: Full emulator host (Avalonia Desktop) with system emulation (C64, etc.)
- **Shared library**: `DebugAdapterLogic` is transport-agnostic, used by both modes

### 3. Async Execution with Breakpoint Evaluator
- **Built-in execution**: Background task loop for standalone hosts (continue/step-over/step-out)
- **External execution**: Emulator host drives the CPU; `DebuggerBreakpointEvaluator` hooks into the run loop
- **Pre-execution checks**: Breakpoints evaluated before each instruction executes

### 4. Minimal VSCode Extension
- **Why**: Most logic in debug adapter library (testable without VSCode)
- **Extension responsibilities**: Process lifecycle, TCP connection management, memory viewer UI, address decorations, config/task generators

## DAP Request/Response Flow

```
1. VSCode → initialize → Adapter
   Adapter → InitializeResponse (capabilities incl. supportsConditionalBreakpoints,
     supportsDisassembleRequest, supportsReadMemoryRequest, supportsWriteMemoryRequest,
     supportsSetVariable, supportsSetExpression, supportsEvaluateForHovers,
     supportsInstructionBreakpoints, supportsFunctionBreakpoints,
     supportsGotoTargetsRequest, supportsSteppingGranularity, etc.)
   Adapter → initialized event

2. VSCode → launch/attach → Adapter
   Launch: Adapter loads .prg/.bin, parses .dbg symbols, creates/binds CPU
   Attach: Adapter connects to running emulator, loads debug symbols
   Adapter → stopped event (if stopOnEntry=true, deferred until configurationDone)

3. VSCode → setBreakpoints(source, breakpoints[]) → Adapter
   Adapter resolves source lines to addresses via .dbg mappings
   VSCode → setInstructionBreakpoints / setFunctionBreakpoints → Adapter
   Adapter stores per-category breakpoints (with optional conditions)

4. VSCode → configurationDone → Adapter
   Deferred stopOnEntry stopped event fires (if pending)

5. VSCode → threads → Adapter
   Adapter → [Thread(1, "6502 CPU")]

6. VSCode → stackTrace(threadId) → Adapter
   Adapter → [StackFrame with source location (if .dbg) or disassembly]

7. VSCode → scopes(frameId) → Adapter
   Adapter → [Scope("Registers"), Scope("Flags"), Scope("Labels"), Scope("Constants")]

8. VSCode → variables(scopeRef) → Adapter
   Adapter → [Variable list for registers, flags, labels, or constants]

9. VSCode → evaluate(expression, context) → Adapter
   Handles: registers, hex/dec addresses, symbol names, REPL commands (dump, set)

10. VSCode → continue/next/stepIn/stepOut → Adapter
    next: Detects JSR → temp breakpoint at PC+3, else single step
    stepOut: Enables StepOutMode → runs until RTS
    continue: Resumes execution loop
    Adapter → stopped event (with reason + hitBreakpointIds)

11. VSCode → readMemory/writeMemory → Adapter
    Standard DAP memory access (base64 encoded)

12. VSCode → disassemble → Adapter
    Full 64K address space walk for consistent instruction boundaries

13. VSCode → gotoTargets/goto → Adapter
    Resolves source line to address, sets PC

14. VSCode → disconnect → Adapter
    Adapter → terminated event, OnExit fired
```

## Current Limitations

1. **Single stack frame**: Only the top frame (current PC) is shown; no call stack reconstruction
2. **No reverse debugging**: Forward execution only
3. **No hot reload**: Must restart to reload program
4. **No data breakpoints**: Cannot watch memory addresses for changes
5. **ca65-only debug symbols**: Only ca65 `.dbg` format is supported (no `.lst`, `.sym`, KickAssembler, ACME, DASM debug formats)

## Future Enhancements

### High Priority
- [x] Source-level debugging (ca65 `.dbg` file support)
- [x] Memory view/editing (DAP readMemory/writeMemory + custom hex dump viewer)
- [x] Symbol file support (ca65 `.dbg` — labels, constants, segments, multi-file merge)
- [x] Conditional breakpoints
- [ ] Additional debug symbol formats (KickAssembler `.dbg`, ACME, DASM)

### Medium Priority
- [x] Watch expressions (evaluate registers, addresses, symbols; REPL commands)
- [x] Step over JSR (temp breakpoint at return address)
- [x] Step out (run until RTS)
- [x] Hover to inspect memory addresses and symbols
- [ ] Multiple disassembly lines in stack trace (call stack reconstruction)
- [ ] Stepping granularity for source-level lines (step over multi-instruction source lines)

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
- [x] Step through instructions (step in, step over JSR, step out)
- [x] Continue execution
- [x] Inspect registers and flags
- [x] Edit registers, flags, and memory
- [x] View disassembly
- [x] Source-level debugging with .dbg file
- [x] Memory inspection (read/write/dump)
- [x] Watch expressions and hover evaluation
- [x] Conditional breakpoints
- [x] Emulator mode (TCP) launch and attach
- [x] Goto / Set PC

## Performance Considerations

- **Stepping**: Single-instruction execution for step-in; background task loop for continue/step-over/step-out
- **Disassembly**: Full 64K memory walk on each request; cache-gap-fill logic pre-fills VS Code's cache near address 0
- **Breakpoint evaluation**: Pre-execution check via `DebuggerBreakpointEvaluator` with periodic `Task.Yield()` every 1000 instructions

## Security Considerations

- **File Access**: Reads user-specified `.prg`/`.bin`/`.dbg` files and source files (for hover context)
- **Execution**: Runs in isolated CPU emulator (no system access)
- **Network**: TCP localhost only (emulator mode on configurable port, default 6502)

## Maintenance Notes

### Dependencies
- **C# Debug Adapter Library**: Highbyte.DotNet6502, System.Text.Json
- **C# Console App**: Debug Adapter Library, Highbyte.DotNet6502.Systems.Generic
- **TypeScript Extension**: @vscode/debugadapter, @vscode/debugprotocol, jsonc-parser

### Breaking Changes to Avoid
- DAP protocol messages (maintain backward compatibility)
- Debug adapter executable location/name
- Launch configuration schema

### Logging/Debugging
- C# adapter: File-based logging to `$TMPDIR/dotnet6502-debugadapter.log`
- VSCode extension: `[6502 Debug]` prefixed console output; `[Emulator Host]` for spawned process output
- DAP message tracking via `DebugAdapterTrackerFactory`

## Credits

- **Debug Adapter Protocol**: [Microsoft DAP Specification](https://microsoft.github.io/debug-adapter-protocol/)
- **ca65 Debug Format**: [cc65 documentation](https://cc65.github.io/doc/)
