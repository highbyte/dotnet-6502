# TCP Debug Adapter Integration

## Overview

The debug adapter now supports TCP transport in addition to STDIN/STDOUT, enabling debugging of desktop GUI applications like the Avalonia Desktop app.

## Architecture

### Library Structure

- **Highbyte.DotNet6502.DebugAdapter** (library)
  - `IDebugAdapterTransport` - Interface for transport implementations
  - `StdioTransport` - STDIN/STDOUT implementation for console apps
  - `TcpTransport` - TCP socket implementation for network connections
  - `TcpDebugAdapterServer` - TCP listener that accepts debug connections
  - `DapProtocol` - Debug Adapter Protocol message handling
  - `DebugAdapterLogic` - Core DAP request/response logic
  - `Ca65DbgParser` - Debug symbol parser

### Console Application

- **Highbyte.DotNet6502.DebugAdapter.ConsoleApp**
  - Uses `StdioTransport` for VSCode extension integration
  - Launched by VSCode extension as a child process
  - Maintains backward compatibility with existing VSCode extension

### Desktop Application Integration

- **Highbyte.DotNet6502.App.Avalonia.Desktop**
  - Integrated `TcpDebugAdapterServer` for TCP-based debugging
  - Accepts `--debug-port <port>` command-line argument
  - Accepts `--debug-wait` flag to wait for debugger connection before starting
  - Runs debug adapter server on background thread

## Usage

### Avalonia Desktop App with TCP Debug Adapter

Start the Avalonia Desktop app with debug adapter enabled:

```bash
./Highbyte.DotNet6502.App.Avalonia.Desktop --debug-port 4711
```

To wait for the debugger to connect before starting:

```bash
./Highbyte.DotNet6502.App.Avalonia.Desktop --debug-port 4711 --debug-wait
```

Combined with console logging:

```bash
./Highbyte.DotNet6502.App.Avalonia.Desktop --debug-port 4711 --console-log -l Debug
```

### VSCode Configuration

To debug the Avalonia Desktop app from VSCode, you'll need to add a launch configuration that connects to the TCP port:

```json
{
    "type": "dotnet6502-debug",
    "request": "attach",
    "name": "Attach to Avalonia Desktop",
    "debugServer": 4711,
    "program": "${workspaceFolder}/samples/Assembler/GenericComputer/snake6502/build/snake6502.prg",
    "stopOnEntry": true,
    "trace": true
}
```

**Note:** The VSCode extension may need updates to support the `debugServer` configuration property for TCP connections.

## Implementation Details

### TCP Transport

The `TcpTransport` class implements `IDebugAdapterTransport` using a `TcpClient` and `NetworkStream`:

- Reads DAP messages with Content-Length headers
- Writes DAP messages with proper framing
- Fires `Disconnected` event when connection is closed

### TCP Debug Adapter Server

The `TcpDebugAdapterServer` class:

- Listens on `IPAddress.Loopback` (localhost only)
- Accepts a single client connection at a time
- Fires `ClientConnected` event with a `TcpTransport` instance
- Supports port 0 for random port assignment

### Avalonia Desktop Integration

The Avalonia Desktop app:

1. Parses `--debug-port` and `--debug-wait` command-line arguments
2. Creates a `TcpDebugAdapterServer` when debug port is specified
3. Handles `ClientConnected` event by:
   - Creating `DapProtocol` and `DebugAdapterLogic` instances
   - Starting a message loop on a background thread
   - Logging to a separate file in the temp directory
4. Optionally waits for debugger connection (30 second timeout)
5. Continues with normal application startup

### Debug Logging

Debug adapter activity is logged to:

```
$TMPDIR/dotnet6502-debugadapter-avalonia-{timestamp}.log
```

This log file contains:
- Connection events
- DAP messages sent/received
- Errors and exceptions

## Future Enhancements

1. **VSCode Extension Updates**
   - Add support for `debugServer` configuration property
   - Allow attaching to running desktop applications
   - Provide UI for discovering running instances

2. **Multiple Client Support**
   - Allow multiple simultaneous debug connections
   - Share breakpoints and state across clients

3. **Auto-Discovery**
   - Broadcast availability on local network
   - Allow VSCode to discover running instances

4. **Security**
   - Add authentication token support
   - Support remote debugging with encryption

## Testing

### Manual Testing

1. Start the Avalonia Desktop app with debug port:
   ```bash
   ./Highbyte.DotNet6502.App.Avalonia.Desktop --debug-port 4711 --debug-wait --console-log
   ```

2. Connect with a TCP client (e.g., `nc`):
   ```bash
   nc localhost 4711
   ```

3. Send a DAP initialize request:
   ```
   Content-Length: 122

   {"seq":1,"type":"request","command":"initialize","arguments":{"adapterID":"dotnet6502","pathFormat":"path"}}
   ```

4. Verify response is received

### Automated Testing

Future work: Add integration tests that:
- Start the desktop app with debug port
- Connect via TCP
- Send DAP requests
- Verify responses
- Test breakpoint functionality
