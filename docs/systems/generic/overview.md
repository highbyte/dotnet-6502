# Overview of the Generic system

A generic computer, with configurable screen sizes and IO memory locations.

Core library: [`Highbyte.DotNet6502.Systems.Generic`](../../libraries/system-specific/generic.md).

CPU: configurable CPU model — NMOS **6502** (`nmos6502`, default), MOS **6510** (`mos6510`),
or NCR **65C02** (`ncr65c02`) — plus the undocumented-opcode compatibility profile for the
NMOS-based models.

## Implementation libraries

For the libraries used to render and accept input, see [Libraries](libraries.md).
