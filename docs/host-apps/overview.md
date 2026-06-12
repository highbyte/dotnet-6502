# Overview of host apps

The emulator runs in several host apps. Some run natively on the desktop; some run entirely in the
browser via WebAssembly (no install, just open the URL). **Avalonia does both** — the Avalonia
Browser and Desktop apps share almost all code, including the UI, so they are documented together as
one app with two runtimes.

| App | Runtime | UI / rendering | Audio | Use case |
|-----|---------|----------------|-------|----------|
| [Avalonia](avalonia/overview.md) | Browser + Desktop | [Avalonia UI](https://avaloniaui.net/) (Skia-based) | NAudio (OpenAL on desktop, WebAudio interop in the browser). C64 sample-based (default) and command-stream providers. | Default app; most features (scripting, debugger, remote control on desktop; URL automation in the browser). |
| [Blazor WebAssembly](blazor-wasm/overview.md) | Browser | [Blazor WebAssembly](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor) + `Highbyte.DotNet6502.Impl.Skia` (SkiaSharp on canvas) | `Highbyte.DotNet6502.Impl.AspNet` (WebAudio JS interop). C64 command-stream provider only. | SkiaSharp-rendered browser alternative. |
| [SadConsole](sadconsole/overview.md) | Desktop | [SadConsole](https://github.com/Thraka/SadConsole) (terminal/ASCII engine) | NAudio + OpenAL. | Console-style retro UI. |
| [SilkNetNative](silknet-native/overview.md) | Desktop | [Silk.NET](https://github.com/dotnet/Silk.NET) + ImGui + OpenGL/SkiaSharp | NAudio + OpenAL. | OpenGL/shader rendering paths, incl. a custom GPU-packet C64 renderer. |
| [Terminal (TUI)](terminal/overview.md) | Desktop / CLI | [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) v2 — renders the emulated text-mode screen as colored Unicode cells in a real terminal (works over SSH, in tmux). | none | Interactive emulator inside a real terminal. C64 and VIC-20 (text mode); no audio. |
| [Console Monitor](console-monitor/overview.md) | Desktop | Plain .NET console | none | Stand-alone 6502 machine code monitor; no system emulation UI. |
| [Headless](headless/overview.md) | Desktop / CLI | none | none | Automation, scripting, CI workflows. Driven entirely by CLI args and Lua. |

## Avalonia: one app, two runtimes

The Avalonia Browser and Desktop apps share the same emulator core *and* almost all UI code, so the
same UI is validated on both targets. Their feature documentation is shared on the per-system pages
([C64](avalonia/c64.md), [Generic](avalonia/generic.md)), with the few Browser-vs-Desktop
differences called out inline. The Blazor app predates the Avalonia browser target and remains as
the SkiaSharp-rendered alternative.

## Installation

Pre-built binaries for the desktop apps are available via Homebrew, Scoop, or manual download. See
[Installation](installation.md) (covers Avalonia Desktop, Terminal (TUI), and Headless). The browser apps need no
install — just open the live version.

For automation and scripting features available across these apps, see [Tools](../tools/overview.md).

## Platform support

See per-app troubleshooting pages for platform-specific notes:

- [Avalonia Desktop troubleshooting](avalonia/troubleshooting.md) — Windows / macOS / Linux all working (Linux ARM64 needs a freetype workaround).
- [SilkNetNative troubleshooting](silknet-native/troubleshooting.md) — requires OpenGL; ARM64 Linux/Windows not currently working.
- [SadConsole troubleshooting](sadconsole/troubleshooting.md) — ARM64 Linux/Windows not currently working.
- [Terminal (TUI)](terminal/overview.md) — cross-platform; needs a terminal with Unicode and 24-bit ("true color") support. Runs over SSH and in `tmux`/`screen`.

## Limitations specific to browser runtimes

- Lua TCP client (`tcp` global) is unavailable — `System.Net.Sockets.TcpClient` is not supported in WebAssembly.
- Lua filesystem access (`file` / `emu.load`) is sandboxed; the key/value `store` falls back to `localStorage`.
- No CLI arguments, no VS Code debug adapter, no remote control endpoint — those are desktop-only features. The Avalonia Browser app does support URL query parameters for automated startup and script injection; see [Avalonia Browser app](avalonia/browser.md#url-query-parameters).

For general project limitations, see [Limitations](../home/limitations.md).
