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
- **Avalonia Browser**: supported via a WebSocket bridge endpoint
- **Blazor WASM**: not supported

Browser-hosted apps cannot open raw TCP sockets directly, so they use a WebSocket endpoint that
bridges to the fixed TCP destination on the server side. For the Avalonia Browser host, configure
the SwiftLink bridge URL to a `ws://` or `wss://` endpoint, such as the Cloudflare Worker bridge in
`tools/cloudflare/swiftlink-bridge/`.

The browser host can also append an optional logical `target` id to the bridge URL. The Worker then
maps that id to an allowlisted `host` / `port` / `tls` destination in its own config.

In the Avalonia Browser C64 config UI, that browser-side `target` is selected from a combo box fed
by the host config's `SwiftLinkBridgeTargetIds` list. The current defaults include
`compunet-reborn` for Compunet testing and `local-echo` for local bridge smoke tests.

Current Avalonia Browser defaults are chosen to work with the deployed Compunet bridge:

- `Bridge URL`: `wss://ws-tcp-bridge.highbyte.workers.dev/bridge`
- `Target ID`: `compunet-reborn`
- `Transport mode`: `HayesModem`
- `Interrupt line`: `NMI`

For local bridge development with `wrangler dev`, temporarily override the browser config to:

- `Bridge URL`: `ws://127.0.0.1:8787/bridge`
- `Target ID`: `compunet-reborn` for real Compunet testing, or `local-echo` for echo smoke tests

## Config notes

- `Connect automatically when emulator starts` is useful for `RawTcp` mode.
- In the Avalonia Browser host, `RawTcp` means `WebSocket bridge to a fixed TCP target`, not a
  direct browser TCP socket.
- In the Avalonia Browser host, the bridge can either use one default target or a logical target id
  chosen by the client and resolved against a Worker-side allowlist.
- In `HayesModem` mode, the C64 software is expected to dial using `ATDT...`, so auto-connect does
  not establish the remote session on its own.
- In the Avalonia Browser host, `HayesModem` mode keeps modem command parsing in the emulator and
  uses the WebSocket bridge only as the fixed-target data path after the software dials.
- `Compatible` receive mode is recommended for real third-party software.
- `FastBuffered` is mainly intended for custom raw TCP integrations where lower latency matters
  more than strict compatibility.

## Related docs

- [overview.md](./overview.md)
- [compatible-programs.md](./compatible-programs.md)
- [avalonia-desktop.md](./../../desktop-apps/avalonia-desktop.md)
- [avalonia-browser.md](./../../web-apps/avalonia-browser.md)
