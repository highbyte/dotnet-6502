# Overview of web apps

Web apps run the emulator entirely in the browser via WebAssembly — no install, just open the URL. There are two implementations using different .NET UI frameworks.

| App | UI framework | Rendering | Audio | Live version |
|-----|--------------|-----------|-------|---------------|
| [Avalonia Browser app](avalonia-browser.md) | [Avalonia UI](https://avaloniaui.net/) | `Highbyte.DotNet6502.Impl.Avalonia` | `Highbyte.DotNet6502.Impl.NAudio` (WebAudio interop) | <https://highbyte.se/dotnet-6502/app2> |
| [Blazor Web Assembly app](blazor-wasm.md) | [Blazor WebAssembly](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor) | `Highbyte.DotNet6502.Impl.Skia` (SkiaSharp on canvas) | `Highbyte.DotNet6502.Impl.AspNet` (custom WebAudio JS interop) | <https://highbyte.se/dotnet-6502/app> |

## Why two?

The two apps share the same emulator core — they differ in UI/rendering tech. The Avalonia Browser app shares almost all UI code with the [Avalonia Desktop app](../desktop-apps/avalonia-desktop.md), making it a natural way to validate the same UI on both targets. The Blazor app predates the Avalonia browser target and remains as the SkiaSharp-rendered alternative.

## Limitations specific to browser apps

- Lua TCP client (`tcp` global) is unavailable — `System.Net.Sockets.TcpClient` is not supported in WebAssembly.
- Lua filesystem access (`file` / `emu.load`) is sandboxed; the key/value `store` falls back to `localStorage`.
- No CLI arguments, no VS Code debug adapter, no remote control endpoint — those are desktop-only features. The Avalonia Browser app does support URL query parameters for automated startup and script injection; see [Avalonia Browser app](avalonia-browser.md#url-query-parameters).

For general project limitations, see [Limitations](../home/limitations.md).
