# Overview of remote control

The emulator supports a persistent TCP remote control endpoint that lets external processes inspect and drive a running emulator instance in real time. It is designed for automation, AI agent integration, and tooling that needs ad-hoc access without embedding a Lua script inside the emulator process.

Implementation library: [`Highbyte.DotNet6502.Remoting`](../../libraries/core/dotnet6502-remoting.md).

Key design points:

- **Persistent TCP connection** — one client at a time; the server accepts a new client after the previous one disconnects.
- **Newline-delimited JSON** — every request is a single JSON object terminated by `\n`; every response is a single JSON object terminated by `\n`.
- **Platform-agnostic** — the same protocol works against both the [Avalonia Desktop app](../../desktop-apps/avalonia-desktop.md) and the [Headless app](../../desktop-apps/headless.md).
- **Non-exclusive** — user input from keyboard/joystick and remote input coexist; neither locks the other out.
- **Frame-synchronized input** — joystick, keyboard, and memory-write commands are queued and executed at the next frame boundary so they do not race with the CPU.

For the protocol and command reference, see [TCP protocol](tcp-protocol.md). For automation recipes, see [Examples](examples.md). For the bundled CLI client, see [Remote Client console app](remote-client.md).

## Starting the Emulator with Remote Control

### Avalonia Desktop

**Option 1 — command line (server starts automatically on launch):**

```sh
# Start on a fixed port (loopback only — 127.0.0.1 — by default)
dotnet run --project src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Desktop -- --remote-port 6510

# Or via the published binary
./Highbyte.DotNet6502.App.Avalonia.Desktop --remote-port 6510

# Bind to a specific interface to accept remote connections
./Highbyte.DotNet6502.App.Avalonia.Desktop --remote-port 6510 --remote-bind-address 0.0.0.0
```

**Option 2 — from the UI** (start/stop at any time without restarting the app):

1. Open the **Debug & Remoting tab** (via the `View` menu → `Debug & Remoting`).
2. In the *Remote Control Server* section, enter the desired **bind address** (default `127.0.0.1`) and **port** number.
3. Click **Start** to begin listening.

When the server is listening, the **Debug & Remoting tab** shows the *Remote Control Server* status as `Listening on 127.0.0.1:6510`. When a client connects, a blue banner appears at the bottom of the window: `• Remote Control Connected (port 6510)`.

### Headless

```sh
# Loopback only (default)
dotnet run --project src/apps/Highbyte.DotNet6502.App.Headless -- \
  --remote-port 6510 \
  --system C64 --start

# Bind to all network interfaces so the server is reachable from another device
dotnet run --project src/apps/Highbyte.DotNet6502.App.Headless -- \
  --remote-port 6510 --remote-bind-address 0.0.0.0 \
  --system C64 --start

# Allow the emu.quit command (disabled by default on headless too unless opted in)
dotnet run --project src/apps/Highbyte.DotNet6502.App.Headless -- \
  --remote-port 6510 --allow-remote-quit \
  --system C64 --start
```

### Bind address (`--remote-bind-address`)

By default the server binds to `127.0.0.1`, so it is only reachable from the same machine. Pass `--remote-bind-address <ip>` (or set the **Bind** field in the *Remote Control Server* section of the *Debug & Remoting* tab) to change this.

| Value           | Meaning                                                                  |
|-----------------|--------------------------------------------------------------------------|
| `127.0.0.1`     | Loopback only — same machine (default)                                   |
| `0.0.0.0`       | Any IPv4 interface — reachable over the network                          |
| `192.168.x.y`   | A specific LAN IP on the host                                            |
| `::1`           | IPv6 loopback                                                            |
| `::`            | Any IPv6 interface                                                       |

!!! warning
    **The protocol is unauthenticated.** Any client that can reach the bind address can fully drive the emulator (inject input, read/write memory, etc.). Only bind to non-loopback addresses on networks you trust.

## Limitations

- **Frame-boundary commands require the emulator to be running.** `mem.write`, `cpu.set`, `keyboard.press/release/releaseall`, `joystick.set/press/release/releaseall`, `c64.type`, and `c64.loadprg` return an immediate error if the emulator state is `Paused` or `Uninitialized`. Use `emu.state` to confirm `Running` before sending these commands, or send `emu.start` first.
- **There is no `emu.resume` command.** `emu.start` serves dual purpose: it starts the emulator from `Uninitialized` *and* resumes from `Paused`. The existing system state is preserved on resume; use `emu.reset` for a hard restart.
- **`emu.selectsystem` and `emu.selectvariant` require the emulator to be stopped** (`Uninitialized`). Call `emu.stop` first, then select, then `emu.start`.
- **One client at a time.** A second connection attempt is accepted only after the first client disconnects.
- **`emu.quit` is disabled in Avalonia Desktop** by default. It is available in headless mode when `--allow-remote-quit` is passed.
- **`screenshot` returns an error in headless mode** because no renderer is active.
- **Loopback by default.** The server binds to `127.0.0.1` unless `--remote-bind-address` (or the **Bind** field in the Debug & Remoting tab) is set to a different interface. The wire protocol is unauthenticated — only bind to non-loopback addresses on trusted networks.
- **`keyboard.press` holds a key until `keyboard.release` or `keyboard.releaseall`.** The client controls press duration by choosing when to release. Keys are applied at frame boundary and remain held until released.
- **`joystick.press` holds joystick actions until `joystick.release` or `joystick.releaseall`.** Use this for ergonomic hold/release remote control.
- **`c64.type` and `c64.loadprg` are C64-specific.** Other systems do not implement these and will return an error. The text/PRG is applied at the next frame boundary.
- **`c64.type` feeds text across frames** — if the C64 keyboard buffer is full the remaining characters wait until space is available.
- **Injected joystick actions from `joystick.set` are not persistent** — they must be resent every frame to hold a direction. Use `joystick.press` if you want stateful joystick hold/release behavior.
