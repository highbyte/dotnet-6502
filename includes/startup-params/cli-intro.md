There are two **mutually exclusive** ways to drive startup from the command line:

- **Scripting mode** — a Lua script selects the system and controls start, load, and lifecycle.
- **Automated startup mode** — CLI flags select a system, optionally load a program, and start it.

Parameters are grouped below into **General parameters** (system-agnostic — interpreted by the
shared startup pipeline, valid for any system) and one group per **system** (interpreted by that
system's plugin; currently only **C64**).

The **Depends on** column lists each parameter's requirements and any parameters it is mutually
exclusive with. Availability differs between the Avalonia Desktop app and the Headless app where
noted; an *(Avalonia Desktop only)* / *(Headless only)* marker means the flag is ignored by the
other app.

!!! important
    `--script` / `--scriptDir` are **mutually exclusive** with all automated-startup parameters
    (`--system`, `--systemVariant`, `--start`, `--waitForSystemReady`, `--loadPrg`, `--loadPrgUrl`,
    `--runLoadedProgram`, and the C64-specific parameters). In scripting mode the Lua script is
    responsible for all emulator setup and lifecycle; combining the two modes is an error.
