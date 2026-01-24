# Known Issues and Limitations

## UI Behavior

### Disassembly View Scroll Position After Large PC Jumps

**Symptom**: When the Program Counter (PC) jumps significantly (e.g., from 0x060e to 0x0000), the disassembly view correctly highlights the new PC address, but may not auto-scroll to show it. The highlighted instruction may be off-screen.

**Cause**: This is a VS Code disassembly view limitation. When the debug adapter returns synthetic "padding" addresses to satisfy DAP protocol requirements for instructions before address 0x0000, VS Code's scroll positioning can become confused.

**Workaround**: Manually scroll the disassembly view to see the highlighted instruction after a large PC jump.

**Technical details**: The 6502 address space starts at 0x0000. When VS Code requests "50 instructions before address 0x0000", we must return synthetic placeholder instructions since there's nothing before address 0. These placeholders use high addresses (0xffce, etc.) which can affect VS Code's scroll calculations.

### Variables Panel Clears After Stepping

**Symptom**: After pressing F10 (step), the VARIABLES panel becomes empty until you click on "current" in the CALL STACK panel again.

**Cause**: This is **normal VSCode behavior** for all debug adapters. When a step completes:
1. Debugger sends "stopped" event
2. VSCode re-queries the stack trace
3. VSCode **deselects** the current frame by default
4. Variables only show when a frame is selected

**Workaround**: Click on the "current" entry in CALL STACK after each step to re-select it and populate variables.

**Why we can't fix this**: This is VSCode's UI behavior, not something the debug adapter can control. The DAP protocol has no way to tell VSCode "keep this frame selected" or "auto-select the first frame". Other debuggers (gdb, lldb, etc.) have the same behavior.

**Future improvement**: We could potentially add a VSCode extension command or configuration to auto-select the frame, but that would require changes to the extension, not the debug adapter.

## MVP Limitations (By Design)

### No Source-Level Debugging
- Must use memory addresses instead of source line numbers
- Line number in breakpoints = hex memory address
- Example: Line 1536 = address 0x0600

### No Memory Inspection
- Can only view registers and flags
- No memory view/editor yet

### Address-Only Breakpoints
- No conditional breakpoints
- No data breakpoints (memory watchpoints)
- No breakpoint conditions or hit counts

### Simple Step Semantics
- Step In/Out/Over all do the same thing (single instruction step)
- No "step over JSR" (subroutine calls)
- No call stack tracking beyond current PC

### Single Thread Model
- Always one thread: "6502 CPU"
- No support for debugging multiple systems/CPUs simultaneously

## Not Issues (Expected Behavior)

### "current" in Call Stack
This is the only stack frame because 6502 code runs in a single execution context. There's no call stack in the traditional sense - just the current instruction at PC.

### Frame ID 0
Using frame ID 0 for the top/only frame is standard DAP practice.

### Breakpoint Line Numbers
Line numbers correspond to memory addresses by design in MVP. This is intentional for simplicity. Future versions will add source file support.

## Workarounds

### Keep Variables Visible
After each step (F10), click "current" in CALL STACK to re-select the frame and refresh variables.

### Better: Use Debug Console
You can see register values in the Debug Console output if you add logging to the debug adapter (future enhancement).

### Alternative: Use Step + Hover
Some debuggers let you hover over values to inspect them. This could be added in the future for register names in the disassembly.
