# NAudio

Library: `Highbyte.DotNet6502.Impl.NAudio`

- A **system-agnostic audio target** (`IAudioCommandTarget`) implemented with the [`NAudio`](https://github.com/naudio/NAudio) audio library, using a custom `Silk.NET.OpenAL` provider for cross-platform support. Can be used from all native applications.

This library carries no system-specific code — the audio command vocabulary was generalised, so a
host app registers the NAudio target for any system that declares an `IAudioProvider`. (The former
`Impl.NAudio.Commodore64` library was removed.)

## Audio

TODO

