<h1 align="center">Highbyte.DotNet6502 systems</h1>

# Overview
_Systems_ are implementations of the  [`ISystem`](../src/libraries/Highbyte.DotNet6502/Systems/ISystem.cs) and related interfaces, and represent a computer running on the 6502 CPU using the [`Highbyte.DotNet6502`](CPU_LIBRARY.md) library for executing code.

A system implementation does not have any dependencies to specific rendering, input handling, or audio technologies. For these purposes, see the [`Highbyte.DotNet6502.Impl.*`](RENDER_INPUT_AUDIO.md) projects.

# Systems
- [Commodore 64](SYSTEMS_C64.md)
- [Generic computer](SYSTEMS_GENERIC.md)
