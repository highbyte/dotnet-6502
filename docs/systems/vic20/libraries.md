# Libraries used by the VIC-20 system

## Core library

The VIC-20 system logic — VIC-I display, VIA chips, keyboard matrix — lives in:

- [`Highbyte.DotNet6502.Systems.Vic20`](../../libraries/system-specific/vic20.md)
  · [source](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Systems.Vic20)

This library has no UI, rendering, or I/O dependencies. It exposes abstractions that the implementation libraries below plug into.

## Implementation libraries

VIC-20-specific host code lives in its own **engine-plugin** libraries, one per host technology,
named `Highbyte.DotNet6502.Impl.<Tech>.Vic20`. Each carries the VIC-20 host config and an
`ISystemEnginePlugin` that registers the VIC-20 with the host app's DI container. Host apps
**discover these plugins at runtime** — see
[`Highbyte.DotNet6502.Systems.Plugins`](../../libraries/core/dotnet6502-systems-plugins.md) — and
hold no direct project reference to them.

| Engine-plugin library | Host technology | Used by app |
| --------------------- | --------------- | ----------- |
| `Highbyte.DotNet6502.Impl.Avalonia.Vic20` | Avalonia | Avalonia Desktop, Avalonia Browser |
| `Highbyte.DotNet6502.Impl.Terminal.Vic20` | Terminal.Gui (text cells) | Terminal (TUI) |

The VIC-20 has Avalonia and Terminal (TUI) engine plugins. No SilkNet, SadConsole, Blazor WASM, or
Headless plugins exist yet.

### Render

The VIC-20 now exposes two host-agnostic render paths:

- [`Vic20Rasterizer`](https://github.com/highbyte/dotnet-6502/blob/master/src/libraries/Highbyte.DotNet6502.Systems.Vic20/Render/Vic20Rasterizer.cs)
  for pixel-accurate character rendering from VIC-I memory.
- [`Vic20VideoCommandStream`](https://github.com/highbyte/dotnet-6502/blob/master/src/libraries/Highbyte.DotNet6502.Systems.Vic20/Render/Vic20VideoCommandStream.cs)
  for lightweight glyph-based rendering.

Avalonia can consume both through the generic render targets in
[`Highbyte.DotNet6502.Impl.Avalonia`](../../libraries/implementation/avalonia.md):

- `AvaloniaBitmapTwoLayerRenderTarget` for the rasterizer path.
- `AvaloniaCommandTarget` for the command-stream path.

The Terminal (TUI) app consumes the command-stream (glyph-based) path via the generic terminal
render target in [`Highbyte.DotNet6502.Impl.Terminal`](../../libraries/implementation/terminal.md),
rendering the VIC-20 in character mode as text cells.

### Input

VIC-20 keyboard handling follows the same pattern as the C64. A host-agnostic
[`Vic20InputHandler`](https://github.com/highbyte/dotnet-6502/blob/master/src/libraries/Highbyte.DotNet6502.Systems.Vic20/Input/Vic20InputHandler.cs)
(with
[`Vic20HostKeyboard`](https://github.com/highbyte/dotnet-6502/blob/master/src/libraries/Highbyte.DotNet6502.Systems.Vic20/Input/Vic20HostKeyboard.cs)
/ `Vic20InputConfig`) lives in the VIC-20 system core under `Input/`; each host only supplies a
small native-key &rarr; `HostKey` translation table inside its own input context. The decoder
lives in the engine-plugin library
[`Highbyte.DotNet6502.Impl.Avalonia.Vic20`](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Impl.Avalonia.Vic20).

### Audio

The VIC-20 system does not currently produce audio. No audio provider or target code exists.

For the cross-system view (which app uses which library), see the
[Implementation libraries overview](../../libraries/implementation/overview.md).
