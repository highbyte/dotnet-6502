# Debug adapter

Library: `Highbyte.DotNet6502.DebugAdapter`

Implements the [Debug Adapter Protocol (DAP)](https://microsoft.github.io/debug-adapter-protocol/) for the 6502 emulator over both STDIO (used by the VSCode extension's spawned console process) and TCP (used by long-running GUI apps such as the Avalonia Desktop app).

For the user-facing debugging guide, see [Tools / VSCode debugger / Debugging](../../tools/vscode-debugger/debugging.md).

## Architecture

### Library structure

`Highbyte.DotNet6502.DebugAdapter`:

- `IDebugAdapterTransport` — interface for transport implementations.
- `StdioTransport` — STDIN/STDOUT implementation for console apps.
- `TcpTransport` — TCP socket implementation for network connections.
- `TcpDebugAdapterServer` — TCP listener that accepts debug connections.
- `DapProtocol` — Debug Adapter Protocol message handling.
- `DebugAdapterLogic` — core DAP request/response logic.
- `Ca65DbgParser` — debug symbol parser.

### Console application

`Highbyte.DotNet6502.DebugAdapter.ConsoleApp`:

- Uses `StdioTransport` for VSCode extension integration.
- Launched by the VSCode extension as a child process.
- Maintains backward compatibility with the existing VSCode extension.
- Debugs raw 6502 assembly (ca65), not any specific machine. It hosts a `GenericComputer` purely
  as a bare `ISystem` — a CPU + empty 64 KB RAM, no ROM — and in `builtInExecution` mode the
  adapter steps the CPU directly, touching only `ISystem.CPU` / `ISystem.Mem`. The `DebugAdapter`
  library itself is fully `ISystem`-agnostic. This app is intentionally single-system and not
  routed through plugin discovery.

### Desktop application integration

`Highbyte.DotNet6502.App.Avalonia.Desktop`:

- Integrates `TcpDebugAdapterServer` for TCP-based debugging.
- Accepts `--enableExternalDebug` to enable the TCP debug server.
- Accepts `--debug-port <port>` command-line argument.
- Accepts `--debug-bind-address <ip>` (defaults to `127.0.0.1`).
- Accepts `--debug-wait` to wait for debugger connection before starting.
- Runs the debug adapter server on a background thread.

## Implementation details

### TCP transport

The `TcpTransport` class implements `IDebugAdapterTransport` using a `TcpClient` and `NetworkStream`:

- Reads DAP messages with `Content-Length` headers.
- Writes DAP messages with proper framing.
- Fires `Disconnected` event when the connection is closed.

### TCP debug adapter server

The `TcpDebugAdapterServer` class:

- Listens on a configurable bind address (default: `127.0.0.1`, loopback only).
- Accepts a single client connection at a time.
- Fires `ClientConnected` event with a `TcpTransport` instance.
- Supports port `0` for random port assignment.

### Avalonia Desktop integration

The Avalonia Desktop app:

1. Parses `--enableExternalDebug`, `--debug-port`, `--debug-bind-address`, and `--debug-wait` command-line arguments.
2. Creates a `TcpDebugAdapterServer` when external debug is enabled.
3. Handles `ClientConnected` event by:
    - Creating `DapProtocol` and `DebugAdapterLogic` instances.
    - Starting a message loop on a background thread.
    - Logging to a separate file in the temp directory.
4. Optionally waits for debugger connection (30-second timeout).
5. Continues with normal application startup.

### Debug logging

Debug adapter activity is logged to:

```
$TMPDIR/dotnet6502-debugadapter-avalonia-{timestamp}.log
```

This log file contains:

- Connection events
- DAP messages sent/received
- Errors and exceptions
