# Avalonia

*This page is about a library, not the Avalonia app.*

Library: `Highbyte.DotNet6502.Impl.Avalonia`

- System-agnostic Avalonia render targets (a bitmap two-layer render target) and input context.
  Used by both the Avalonia Desktop and Avalonia Browser apps.

!!! note "System-specific code lives in companion libraries"
    This library holds only **system-agnostic** Avalonia glue. Per-system code is in the
    engine-plugin libraries `Highbyte.DotNet6502.Impl.Avalonia.Commodore64` and
    `Highbyte.DotNet6502.Impl.Avalonia.Generic`. The C64 and Generic systems both render via the
    generic Avalonia bitmap render target — there is no bespoke per-system renderer; the Generic
    input handler lives in `Impl.Avalonia.Generic`. See
    [Systems / C64 / Libraries](../../systems/c64/libraries.md) and
    [Systems / Generic / Libraries](../../systems/generic/libraries.md).

## Render

### Common render targets

TODO

## Input

TODO

## Logging
