# Silk.NET

*This page is about a library, not the SilkNetNative app.*

Library: `Highbyte.DotNet6502.Impl.SilkNet`

- Input context implemented with the [`Silk.NET`](https://github.com/dotnet/Silk.NET) windowing library. Can be used from a native Silk.NET application.
- Render targets implemented with Silk.NET OpenGL bindings, together with custom shaders. Can be used from a native Silk.NET application.

!!! note "System-specific code lives in companion libraries"
    This library holds only **system-agnostic** Silk.NET glue. Per-system code is in the
    engine-plugin libraries `Highbyte.DotNet6502.Impl.SilkNet.Commodore64` (C64 OpenGL render
    target, C64 gamepad) and `Highbyte.DotNet6502.Impl.SilkNet.Generic` (Generic input handler).
    See [Systems / C64 / Libraries](../../systems/c64/libraries.md) and
    [Systems / Generic / Libraries](../../systems/generic/libraries.md).

## Render

### Common render targets

TODO

## Input

TODO
