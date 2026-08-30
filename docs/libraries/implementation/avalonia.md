# Avalonia

*This page is about a library, not the Avalonia app.*

Library: `Highbyte.DotNet6502.Impl.Avalonia`

- System-agnostic Avalonia render targets (a bitmap two-layer render target) and input context.
  Used by both the Avalonia Desktop and Avalonia Browser apps.

!!! note "System-specific code lives in companion libraries"
    This library holds only **system-agnostic** Avalonia glue. Per-system code is in the
    engine-plugin libraries `Highbyte.DotNet6502.Impl.Avalonia.Commodore64`, `.Vic20`, `.Apple2`,
    `.Oric`, and `.Generic`. Their host-agnostic system providers feed the generic Avalonia bitmap
    or command-stream targets, while each plugin wires its system configuration and input handler.
    See [Systems / C64 / Libraries](../../systems/c64/libraries.md),
    [Systems / VIC-20 / Libraries](../../systems/vic20/libraries.md),
    [Systems / Oric / Libraries](../../systems/oric/libraries.md), and the
    [Apple II system overview](../../systems/apple2/overview.md).

## Render

### Common render targets

TODO

## Input

TODO

## Logging
