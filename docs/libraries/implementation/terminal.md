# Terminal

*This page is about a library, not the Terminal (TUI) app.*

Library: `Highbyte.DotNet6502.Impl.Terminal`

- Render target and input context implemented with [`Terminal.Gui`](https://github.com/gui-cs/Terminal.Gui)
  v2. Renders a system's text-mode screen as colored Unicode cells in a real terminal, and maps
  terminal key events to emulator input. Used by the [Terminal (TUI) app](../../host-apps/terminal/overview.md).

!!! note "System-specific code lives in companion libraries"
    This library holds only **system-agnostic** terminal glue. Per-system code is in the
    engine-plugin libraries `Highbyte.DotNet6502.Impl.Terminal.Commodore64` and
    `Highbyte.DotNet6502.Impl.Terminal.Vic20` (host config + `ISystemEnginePlugin` registration).
    See [Systems / C64 / Libraries](../../systems/c64/libraries.md) and
    [Systems / VIC-20 / Libraries](../../systems/vic20/libraries.md).

## Render

The render target (`TerminalRenderTarget`) is system-agnostic: it consumes the system's
video-command stream (`IVideoCommand`) into a grid of screen cells (rune + foreground/background
color), using the system's glyph-to-Unicode converter. A manual-invalidation render loop
(`TerminalRenderLoop`) flushes only when the screen changes, so terminal repaint stays throttled and
independent of the emulator frame rate. Reverse-video screen codes are handled by swapping
foreground/background.

## Input

A terminal input context (`TerminalInputHandlerContext`) and key map (`TerminalKeyMap`) translate
Terminal.Gui key events into the emulator's host-key input. There is no audio implementation —
terminals have no audio output.
