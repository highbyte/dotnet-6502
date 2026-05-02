# Debugging

The VSCode debugger extension uses the [Debug Adapter Protocol (DAP)](https://microsoft.github.io/debug-adapter-protocol/) over TCP to debug 6502 programs running in the Avalonia Desktop emulator.

For internals of the debug adapter library, see [`Highbyte.DotNet6502.DebugAdapter`](../../libraries/core/dotnet6502-debugadapter.md).

## VSCode extension

Install the extension and learn how to use it from its documentation:

- [README.md](https://github.com/highbyte/dotnet-6502/blob/master/tools/vscode-extension/README.md) — installation, quick start, features, launch configuration reference
- [DEBUGGING.md](https://github.com/highbyte/dotnet-6502/blob/master/tools/vscode-extension/DEBUGGING.md) — conditional breakpoints, logpoints, hit counts, stepping, memory inspection, register editing, and more
- [REMOTE_DEBUGGING.md](https://github.com/highbyte/dotnet-6502/blob/master/tools/vscode-extension/REMOTE_DEBUGGING.md) — cross-machine debugging with path mappings and remote source fallback

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
