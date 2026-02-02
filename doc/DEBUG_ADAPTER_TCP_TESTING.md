# TCP Debug Adapter Testing Guide

This guide walks through testing the TCP debug adapter integration between VSCode and the Avalonia Desktop app.

## Prerequisites

1. **Build the debug adapter library:**
   ```bash
   dotnet build src/libraries/Highbyte.DotNet6502.DebugAdapter
   ```

2. **Build the Avalonia Desktop app:**
   ```bash
   dotnet build src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Desktop
   ```

3. **Compile the VSCode extension:**
   ```bash
   cd tools/vscode-extension
   npm run compile
   ```

## Test Scenario 1: Attach to Running Avalonia App

This tests the TCP connection from VSCode to a running Avalonia Desktop application.

### Step 1: Start Avalonia Desktop App with Debug Server

From the main workspace root, run the Avalonia Desktop app task with debug server:

1. Open the main workspace folder in VSCode
2. Press `Cmd+Shift+D` (Debug view)
3. Select "Avalonia desktop app with TCP debug server" from the dropdown
4. Press F5 or click the green play button

**Expected behavior:**
- Terminal shows: "Starting TCP debug adapter server on port 4711"
- Terminal shows: "Waiting for debug client to connect (--debug-wait specified)..."
- Avalonia app window appears but waits for debugger

**Alternative (command line):**
```bash
cd src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Desktop/bin/Debug/net10.0
./Highbyte.DotNet6502.App.Avalonia.Desktop --debug-port 4711 --debug-wait --console-log -l Debug
```

### Step 2: Attach VSCode Debugger

1. Open the `tools/vscode-extension` folder in a **new VSCode window**
2. Press F5 to launch "Extension Development Host"
3. In the Extension Development Host window:
   - File → Open Folder → Select `tools/vscode-extension-test`
   - Press `Cmd+Shift+D` (Debug view)
   - Select "Attach to Avalonia Desktop (TCP debug adapter)"
   - Press F5

**Expected behavior:**
- Extension Development Host Debug Console shows:
  ```
  [6502 Debug] createDebugAdapterDescriptor called for session: Attach to Avalonia Desktop (TCP debug adapter)
  [6502 Debug] Using TCP connection to port 4711
  [6502 Debug] ✓ Created DebugAdapterServer for port 4711
  ```
- Avalonia app terminal shows: "Debug client connected, continuing startup"
- Debug adapter log file created: `$TMPDIR/dotnet6502-debugadapter-avalonia-*.log`
- VSCode shows debug toolbar with play/pause/step buttons
- Avalonia app window becomes active

### Step 3: Load and Debug Program

In the Avalonia Desktop app:

1. Use the File menu to load `tools/vscode-extension-test/test-program.prg`
2. In VSCode, open `test-program.asm`
3. Set a breakpoint on line 10 (or any instruction line)
4. In Avalonia app, run the program

**Expected behavior:**
- Program execution stops at breakpoint
- VSCode editor highlights the breakpoint line
- Debug toolbar shows pause state
- Variables panel shows CPU registers
- Call stack shows current PC location

### Step 4: Test Debug Controls

- **Continue (F5):** Program runs until next breakpoint
- **Step Over (F10):** Executes one instruction
- **Step Into (F11):** Same as Step Over (no subroutines in test)
- **Step Out (Shift+F11):** Runs until RTS
- **Stop (Shift+F5):** Disconnects debugger

**Expected behavior:**
- All debug controls work correctly
- VSCode highlights current instruction
- Register values update after each step
- Memory viewer shows correct values

## Test Scenario 2: Traditional Launch Mode (Verify Backward Compatibility)

This ensures the existing STDIN/STDOUT debug adapter still works.

### Step 1: Open Test Workspace

1. Open `tools/vscode-extension` in VSCode
2. Press F5 to launch Extension Development Host
3. In Extension Development Host, open `tools/vscode-extension-test`

### Step 2: Debug with Traditional Launch

1. Press `Cmd+Shift+D` (Debug view)
2. Select "Debug test-program.prg (C64) with built-in load address"
3. Press F5

**Expected behavior:**
- Extension Development Host Debug Console shows:
  ```
  [6502 Debug] createDebugAdapterDescriptor called for session: Debug test-program.prg...
  [6502 Debug] ✓ Using debug adapter: .../Highbyte.DotNet6502.DebugAdapter
  [6502 Debug] ✓ Created DebugAdapterExecutable, returning to VSCode
  ```
- Debug session starts normally
- Can set breakpoints and debug the program
- Works exactly as before (backward compatible)

## Test Scenario 3: Launch Mode with TCP (Optional)

This tests using `debugServer` property in a launch configuration.

### Step 1: Modify Launch Configuration

Edit `tools/vscode-extension-test/.vscode/launch.json` and add:

```json
{
  "type": "dotnet6502",
  "request": "launch",
  "name": "Debug with TCP (launch mode)",
  "debugServer": 4711,
  "program": "${workspaceFolder}/test-program.prg",
  "dbgFile": "${workspaceFolder}/test-program.dbg",
  "stopOnEntry": true
}
```

### Step 2: Start External Debug Server

In a terminal:
```bash
cd src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Desktop/bin/Debug/net10.0
./Highbyte.DotNet6502.App.Avalonia.Desktop --debug-port 4711 --debug-wait
```

### Step 3: Launch Debug Session

1. Select "Debug with TCP (launch mode)" in debug dropdown
2. Press F5

**Expected behavior:**
- Connects to existing TCP server on port 4711
- Debug session works identically to attach mode

## Verification Checklist

### Connection
- [ ] VSCode successfully connects to TCP port 4711
- [ ] Avalonia app detects client connection
- [ ] Debug adapter log file is created
- [ ] No connection errors in Debug Console

### Breakpoints
- [ ] Can set breakpoints in .asm files
- [ ] Breakpoints are hit during execution
- [ ] Editor highlights active breakpoint line
- [ ] Can remove/disable breakpoints

### Stepping
- [ ] Step Over (F10) works correctly
- [ ] Step Into (F11) works correctly
- [ ] Step Out (Shift+F11) works correctly
- [ ] Continue (F5) runs until next breakpoint

### Variables & Registers
- [ ] CPU registers visible in Variables panel
- [ ] Register values update after each step
- [ ] A, X, Y, SP, PC, SR displayed correctly
- [ ] Status flags (N, V, Z, C) shown

### Call Stack
- [ ] Call stack shows current PC
- [ ] Source file and line number displayed
- [ ] Updates correctly during stepping

### Performance
- [ ] No noticeable lag during stepping
- [ ] Breakpoints respond immediately
- [ ] UI remains responsive
- [ ] TCP connection is stable

## Troubleshooting

### "Could not connect to port 4711"

**Cause:** Avalonia app not running or not listening on port 4711

**Solution:**
1. Check Avalonia app is running with `--debug-port 4711`
2. Check console output for "Started listening on port 4711"
3. Verify no other process is using port 4711: `lsof -i :4711`

### "Debug adapter executable not found"

**Cause:** Debug adapter console app not built

**Solution:**
```bash
dotnet build src/apps/Highbyte.DotNet6502.DebugAdapter
```

### Extension not working

**Cause:** Extension not compiled or old version cached

**Solution:**
```bash
cd tools/vscode-extension
npm run compile
# Then restart Extension Development Host (Cmd+R)
```

### Avalonia app crashes on connection

**Cause:** Possible bug in debug adapter logic

**Solution:**
1. Check debug adapter log file: `$TMPDIR/dotnet6502-debugadapter-avalonia-*.log`
2. Look for exception stack traces
3. Check that test-program.prg and test-program.dbg files exist

### Breakpoints not working

**Cause:** Debug symbols not loaded or wrong file paths

**Solution:**
1. Verify `.dbg` file exists and path is correct in launch.json
2. Check debug adapter log for "Loaded X source files" message
3. Ensure .asm file path matches what's in the .dbg file

## Log Files

### VSCode Debug Console
Location: Extension Development Host → Debug Console panel

Shows extension-side connection attempts and debug adapter descriptor creation.

### Avalonia Console Output
Location: Terminal where Avalonia app is running

Shows debug server startup, client connection, and high-level events.

### Debug Adapter Log File
Location: `$TMPDIR/dotnet6502-debugadapter-avalonia-{timestamp}.log`

Example path: `/var/folders/.../T/dotnet6502-debugadapter-avalonia-20260202-143052.log`

Contains detailed DAP message exchange, breakpoint management, and errors.

To find the file:
```bash
ls -ltr $TMPDIR/dotnet6502-debugadapter-avalonia-*.log | tail -1
```

To monitor in real-time:
```bash
tail -f $TMPDIR/dotnet6502-debugadapter-avalonia-*.log
```

## Success Criteria

The TCP debug adapter integration is working correctly if:

1. ✅ Can attach to running Avalonia Desktop app via TCP
2. ✅ Traditional launch mode still works (backward compatible)
3. ✅ Breakpoints can be set and are hit correctly
4. ✅ Stepping through code works (F10, F11, Shift+F11)
5. ✅ CPU registers visible and update correctly
6. ✅ Call stack shows correct location
7. ✅ No errors in debug logs
8. ✅ Connection is stable (no disconnects)
9. ✅ Performance is acceptable (no lag)
10. ✅ Can disconnect cleanly (Shift+F5)

## Known Limitations

1. **Single Connection:** TCP server accepts only one client at a time
2. **Localhost Only:** Server binds to 127.0.0.1 (security)
3. **No Auto-Discovery:** Must manually specify port number
4. **No Reconnect:** If connection drops, must restart debug session
5. **Port Conflicts:** If port 4711 is in use, must choose different port

## Future Enhancements

1. **Auto-Discovery:** Broadcast/discover running instances on local network
2. **Multiple Clients:** Allow simultaneous debug connections
3. **Port Picker UI:** Extension provides UI to select/discover ports
4. **Reconnect Support:** Automatically reconnect on connection loss
5. **Status Indicator:** Show connection status in VSCode UI
