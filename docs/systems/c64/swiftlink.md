# SwiftLink support

The C64 emulator includes **SwiftLink-compatible ACIA support** for software that expects a
SwiftLink cartridge at `$DE00` or `$DF00`.

## What is supported

- Optional SwiftLink cartridge in the C64 config.
- Base address selection: `$DE00` or `$DF00`.
- Interrupt line selection: `IRQ` or `NMI`.
- Receive mode selection:
  - `Compatible` — default, paced for better software compatibility.
  - `FastBuffered` — lower-latency host buffering for custom raw TCP scenarios.
- Two host transport modes:
  - `RawTcp` — direct byte pipe to a configured host and port.
  - `HayesModem` — host-side Hayes-compatible modem layer for modem-style C64 software.

## Hayes modem mode

`HayesModem` mode lets C64 software talk to a host-side modem emulator instead of sending modem
commands to the remote service directly.

Currently implemented:

- `AT`
- `ATZ`
- `AT&F`
- `ATI`
- `ATH`
- `ATDT host:port`
- `CONNECT 1200`
- `NO CARRIER`

This is enough for modem-style software such as **Compunet Reborn** to reach the logon screen on
native hosts.

## Host availability

SwiftLink support depends on the host app exposing a usable TCP transport.

- **Avalonia Desktop**: supported
- **Headless**: supported
- **Avalonia Browser**: not supported
- **Blazor WASM**: not supported

Browser-hosted apps do not currently expose SwiftLink because the browser sandbox does not provide
the native TCP socket behavior used by the desktop/headless transport implementations.

## Config notes

- `Connect automatically when emulator starts` is useful for `RawTcp` mode.
- In `HayesModem` mode, the C64 software is expected to dial using `ATDT...`, so auto-connect does
  not establish the remote session on its own.
- `Compatible` receive mode is recommended for real third-party software.
- `FastBuffered` is mainly intended for custom raw TCP integrations where lower latency matters
  more than strict compatibility.

## Related docs

- [C64 overview](overview.md)
- [Compatible programs](compatible-programs.md)
- [Avalonia Desktop app](../../desktop-apps/avalonia-desktop.md)
- [Avalonia Browser app](../../web-apps/avalonia-browser.md)
