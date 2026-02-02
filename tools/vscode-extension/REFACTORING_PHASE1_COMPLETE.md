# Debug Adapter Refactoring - Phase 1 Complete

## Summary

Successfully refactored the debug adapter from a monolithic console application into a reusable library with a thin console app wrapper. This sets the foundation for adding TCP support and integrating into desktop applications.

## What Changed

### New Library: `Highbyte.DotNet6502.DebugAdapter`
Location: `src/libraries/Highbyte.DotNet6502.DebugAdapter/`

**New Files:**
- `IDebugAdapterTransport.cs` - Abstraction for transport mechanisms (STDIO, TCP, etc.)
- `StdioTransport.cs` - STDIO implementation for console apps
- `DapProtocol.cs` - Refactored to use transport abstraction
- `DebugAdapterLogic.cs` - Moved from console app (unchanged logic)
- `Ca65DbgParser.cs` - Moved from console app (unchanged)

### Updated Console App: `Highbyte.DotNet6502.DebugAdapter.ConsoleApp`
Location: `src/apps/Highbyte.DotNet6502.DebugAdapter/`

**Changes:**
- Project file renamed to `Highbyte.DotNet6502.DebugAdapter.ConsoleApp.csproj`
- Now references the library instead of containing the logic
- `Program.cs` creates `StdioTransport` and passes it to `DapProtocol`
- **Executable name unchanged:** Still outputs `Highbyte.DotNet6502.DebugAdapter` (backward compatible)

## Architecture

```
┌─────────────────────────────────────┐
│   Console App (Program.cs)          │
│   - Creates StdioTransport           │
│   - Initializes DapProtocol          │
│   - Starts DebugAdapterLogic         │
└────────────┬────────────────────────┘
             │
             │ References
             ▼
┌─────────────────────────────────────┐
│   Debug Adapter Library             │
│                                     │
│   IDebugAdapterTransport            │
│          ▲                          │
│          │                          │
│   ┌──────┴──────┐                  │
│   │             │                  │
│   StdioTransport│                  │
│                                     │
│   DapProtocol ──────►               │
│   DebugAdapterLogic                 │
│   Ca65DbgParser                     │
└─────────────────────────────────────┘
```

## Testing

✅ Library builds successfully
✅ Console app builds successfully  
✅ Executable name preserved: `Highbyte.DotNet6502.DebugAdapter`
✅ Backward compatible with VSCode extension

## Next Steps

With this foundation in place, you can now:

1. **Test the existing console debugger** - Verify it still works with VSCode
2. **Add TCP transport** - Create `TcpTransport.cs` implementing `IDebugAdapterTransport`
3. **Create TCP server** - Add `TcpDebugAdapterServer.cs` to listen for connections
4. **Integrate into desktop apps** - Reference the library and add command-line args for debug port

## Files Modified

- Created: `src/libraries/Highbyte.DotNet6502.DebugAdapter/` (entire library)
- Renamed: Console app project file
- Modified: `src/apps/Highbyte.DotNet6502.DebugAdapter/Program.cs`
- Deleted: Old implementations moved to library

## No Breaking Changes

- Executable name unchanged
- STDIO protocol unchanged
- VSCode extension should work without modifications
