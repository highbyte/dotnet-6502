<!--
Shared C64 feature documentation for the Avalonia apps (Browser + Desktop), included via
pymdownx.snippets into docs/host-apps/avalonia/c64.md.

NOT a standalone page — includes/ is the snippets base_path (see mkdocs.yml), not part of docs/.
Runtime differences are expressed inline with `=== "Browser"` / `=== "Desktop"` tabs or
`!!! note "Browser only"` admonitions — never by duplicating the page.

Sections start at ## (the consumer page supplies the # title). See the
includes/avalonia/README.md and decisions/2026-06-10-avalonia-system-docs-structure.md.
-->

## ROMs

The C64 needs the Kernal, Basic and Chargen ROMs. An auto-download option exists (a license
acknowledgement is required). See [Systems / C64 / ROMs](../../systems/c64/roms.md) for details.

=== "Browser"
    Upload the ROM binaries via the C64 config UI, or use the auto-download option.

=== "Desktop"
    Point the app at a directory containing the ROM files. When no directory override is set, the default is `~/Documents/Highbyte/DotNet6502/roms/C64` (or the Windows Documents equivalent). Desktop user changes are saved to the host-specific `appsettings.user.json` overlay and can be changed in the UI, or use the auto-download option.

## Display / renderers

--8<-- "avalonia-c64-renderers.md"

## Input

Keyboard uses `Avalonia.Input.PhysicalKey` (W3C `code`), so both `US` and `Swedish` C64 keyboard
layouts work. The layout can be overridden in the C64 config dialog. See
[Systems / C64 / Keyboard mapping](../../systems/c64/keyboard.md) for the full host-agnostic mapping.

=== "Browser"
    Keyboard via `Avalonia`; gamepad via
    [`Highbyte.DotNet6502.Impl.Browser`](../../libraries/implementation/browser.md). In Chromium
    browsers the keyboard layout is auto-detected via `navigator.keyboard.getLayoutMap()`; other
    browsers fall through to OS culture, then `US`.

=== "Desktop"
    Keyboard via `Avalonia`; joystick via
    [`Highbyte.DotNet6502.Impl.SilkNet.SDL`](../../libraries/implementation/silknet-sdl.md). The
    keyboard layout is auto-detected from the host (Win32 KLID / macOS `TIS*`).

## Audio

Audio via [NAudio](https://github.com/naudio/NAudio). Two C64 audio providers are available: a
sample-based one (good but not perfect accuracy — the default) and a command-stream synthesizer one
(low CPU but inaccurate); switch in the C64 config dialog if you need lower CPU. The SID emulation
mode (`Auto` / `Fast`) is selectable in the same dialog. See
[C64 audio](../../systems/c64/libraries.md#audio).

=== "Browser"
    Playback via a WebAudio JS interop wave player.

=== "Desktop"
    Playback via `OpenAL`.

## SwiftLink

SwiftLink supports both direct raw-byte bridging and a fixed-target Hayes modem workflow for
software such as Compunet Reborn. See [Systems / C64 / SwiftLink support](../../systems/c64/swiftlink.md).

=== "Browser"
    Available through a WebSocket bridge endpoint. The browser C64 defaults target the deployed
    Cloudflare bridge: `wss://ws-tcp-bridge.highbyte.workers.dev/bridge`, target id
    `compunet-reborn`, transport mode `HayesModem`, interrupt line `NMI`. When developing the
    bridge locally with `wrangler dev`, temporarily change the bridge URL to
    `ws://127.0.0.1:8787/bridge`.

=== "Desktop"
    Optional native SwiftLink cartridge support with `RawTcp` and `HayesModem` transport modes.
    This is the native host currently recommended for SwiftLink-based software such as Compunet
    Reborn.

## C64 menu

The C64-specific menu (shared by both runtimes) exposes:

- **Copy / Paste** — copy the running machine's current BASIC listing to the clipboard, or paste
  clipboard text into the running C64 via the keyboard.
- **Disk Drive & .D64 images** — attach/detach a `.d64` disk image, and **Download & Run** a
  curated list of programs (auto-downloads the `.d64`/`.prg` and starts it). See
  [Compatible programs](../../systems/c64/compatible-programs.md).
- **Load / Save** — load and save BASIC `.prg` files, load & start a binary, and load bundled
  assembly / BASIC example programs.
- **Configuration** — active joystick, keyboard-joystick on/off and port, and the full C64 config
  dialog (ROMs, renderer, audio, SwiftLink, and the **Basic AI coding assistant** — enable it
  there; <kbd>F9</kbd> toggles it live while running). See
  [Basic code completion](../../systems/c64/code-completion.md).

## Share link

!!! note "Browser only"
    The browser app can generate a shareable URL that reproduces the current state when opened —
    the inverse of the [URL query parameters](../../host-apps/avalonia/browser.md#url-query-parameters).

The **Share link…** button (below the Copy/Paste buttons) opens a dialog that builds a link for one
of:

- **Current BASIC program** — shares the live BASIC listing (optionally auto-RUN).
- **Selected "Download & Run" program** — shares the remote `.prg`/`.d64` of the program selected in
  the Disk Drive section.

Options: **Auto-RUN after start** and **Include current settings** (audio + keyboard-joystick;
on by default). The generated URL always carries the system variant (e.g. `C64PAL`) so the
recipient gets the same machine. The link contains the **clean** target URL — any CORS proxy is
applied by the running emulator at fetch time, never baked into the link.

Inline-BASIC links are length-checked (the binding limit is the host/CDN serving the app, not the
browser): a warning past ~8,000 characters, and the dialog refuses to share past ~16,000. For a
large BASIC program, host it and share a `basicUrl=` link instead.
