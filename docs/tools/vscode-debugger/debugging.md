# Debugging

The VSCode debugger extension uses the [Debug Adapter Protocol (DAP)](https://microsoft.github.io/debug-adapter-protocol/) over TCP to debug 6502 programs running in the Avalonia Desktop emulator.

For cross-machine debugging, see [Remote debugging](remote-debugging.md). For internals of the debug adapter library, see [`Highbyte.DotNet6502.DebugAdapter`](../../libraries/core/dotnet6502-debugadapter.md).

## Avalonia Desktop app with TCP Debug Adapter

Start the Avalonia Desktop app with debug adapter enabled:

```sh
./Highbyte.DotNet6502.App.Avalonia.Desktop --enableExternalDebug --debug-port 6502
```

To wait for the debugger to connect before starting:

```sh
./Highbyte.DotNet6502.App.Avalonia.Desktop --enableExternalDebug --debug-port 6502 --debug-wait
```

To bind the server to a different interface, add `--debug-bind-address <ip>`. For example, use `0.0.0.0` to accept connections on any local interface:

```sh
./Highbyte.DotNet6502.App.Avalonia.Desktop --enableExternalDebug --debug-port 6502 --debug-bind-address 0.0.0.0 --debug-wait
```

Combined with console logging:

```sh
./Highbyte.DotNet6502.App.Avalonia.Desktop --enableExternalDebug --debug-port 6502 --console-log -l Debug
```

For the full set of debug-adapter CLI flags, see the **CLI arguments → Debug adapter** section on the [Avalonia Desktop](../../desktop-apps/avalonia-desktop.md#cli-arguments) or [Headless](../../desktop-apps/headless.md#cli-arguments) page.

## VSCode Configuration

To debug the Avalonia Desktop app from VSCode, add a launch configuration that connects to the TCP port:

```json
{
    "type": "dotnet6502",
    "request": "attach",
    "name": "Attach to Avalonia Desktop",
    "debugHost": "127.0.0.1",
    "debugPort": 6502,
    "program": "${workspaceFolder}/samples/Assembler/GenericComputer/snake6502/build/snake6502.prg",
    "stopOnEntry": true
}
```

!!! note
    The emulator can bind the debug server to a specific IP via `--debug-bind-address`, and the VS Code extension can connect to a matching host via `debugHost`. Both still default to `127.0.0.1`, which remains the right default for local debugging.

## Manual smoke test

Useful when verifying that the debug adapter starts up correctly without VSCode involved.

1. Start the Avalonia Desktop app with debug port:

   ```sh
   ./Highbyte.DotNet6502.App.Avalonia.Desktop --enableExternalDebug --debug-port 6502 --debug-wait --console-log
   ```

2. Connect with a TCP client (e.g. `nc`):

   ```sh
   nc 127.0.0.1 6502
   ```

   If you started the emulator with `--debug-bind-address 0.0.0.0`, connecting to `127.0.0.1` from the same machine still works.

3. Send a DAP initialize request:

   ```
   Content-Length: 122

   {"seq":1,"type":"request","command":"initialize","arguments":{"adapterID":"dotnet6502","pathFormat":"path"}}
   ```

4. Verify a response is received.
