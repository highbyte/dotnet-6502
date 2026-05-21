# Overview of systems

*Systems* are implementations of the [`ISystem`](https://github.com/highbyte/dotnet-6502/blob/master/src/libraries/Highbyte.DotNet6502.Systems/ISystem.cs) and related interfaces, and represent a computer running on the 6502 CPU using the [`Highbyte.DotNet6502`](../libraries/core/dotnet6502.md) library for executing code.

A system implementation does not have any dependencies on specific rendering, input handling, or audio technologies. For these purposes, see the [Implementation libraries](../libraries/implementation/overview.md).

For the four-tier model and how host apps stay system-agnostic via plugin discovery, see
[Architecture](../architecture.md).

## Systems

- [Commodore 64](c64/overview.md)
- [Generic computer](generic/overview.md)

## Adding a new system

See [Adding a new emulated system](adding-a-system.md) for a step-by-step guide to plugging a new computer (VIC-20, NES, …) into the emulator.
