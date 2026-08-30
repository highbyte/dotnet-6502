# Libraries used by the Oric system

## Core library

The Oric Atmos machine logic — ULA display, VIA, AY sound, keyboard matrix, joystick interfaces,
BASIC support, and TAP transport — lives in:

- [`Highbyte.DotNet6502.Systems.Oric`](../../libraries/system-specific/oric.md)
  · [source](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Systems.Oric)

This library is UI-agnostic. It exposes render, input, and audio providers that host-specific
implementation libraries connect to their native technologies.

## Implementation libraries

Oric-specific host integration is supplied by **engine-plugin** libraries. Host apps discover them
at runtime through
[`Highbyte.DotNet6502.Systems.Plugins`](../../libraries/core/dotnet6502-systems-plugins.md) instead
of referencing Oric from their system-agnostic host code.

| Engine-plugin library | Host technology | Used by app |
| --------------------- | --------------- | ----------- |
| `Highbyte.DotNet6502.Impl.Avalonia.Oric` | Avalonia + NAudio | Avalonia Desktop, Avalonia Browser |
| `Highbyte.DotNet6502.Impl.Terminal.Oric` | Terminal.Gui text cells | Terminal (TUI) |
| `Highbyte.DotNet6502.Impl.Headless.Oric` | No render/input/audio target | Headless |

The Avalonia and Terminal apps also discover system-specific **shell plugins** that contribute the
Oric menu, Information panel, and configuration dialog:

- `Highbyte.DotNet6502.App.Avalonia.Shell.Oric`
- `Highbyte.DotNet6502.App.Terminal.Shell.Oric`

No SilkNetNative, SadConsole, or Blazor WASM Oric plugin exists.

### Render

The system core provides two host-agnostic render paths:

- `OricRasterizer` produces the full pixel display for Avalonia's generic bitmap render target.
- `OricVideoCommandStream` produces a 40 × 28 glyph stream for the generic Terminal render target.
  It preserves text cells, serial colors, inverse, and flashing attributes, but cannot represent
  hi-res pixels, double-height glyph shape, or RAM-defined custom glyph shapes.

### Input

`OricInputHandler`, `OricHostKeyboard`, and `OricInputConfig` live in the Oric system core. Each
interactive host supplies native-key-to-`HostKey` translation through its generic input context;
the Oric handler applies the keyboard matrix, keyboard layout, and joystick mappings.

### Audio

`OricAySampleProvider` produces mono PCM samples for the AY-3-8912. Avalonia connects it to its
generic NAudio sample target (OpenAL on Desktop and WebAudio in Browser). Terminal and Headless
disable audio.

For the cross-system view, see the
[Implementation libraries overview](../../libraries/implementation/overview.md).
