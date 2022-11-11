<h1 align="center">Highbyte.DotNet6502.Monitor</h1>

# Overview
A library that provides a base for common machine code monitor functionality for the [Highbyte.DotNet6502](../Highbyte.DotNet6502/CPU_LIBRARY.md).

The library need to be implemented for a specific UI technology, see the different [host applications ](../Highbyte.DotNet6502.App/APPS.md).

Specific emulated "Systems" (computers) can also implement additional commands that are specific for that System, see [here](#system-specific-commands).

# General monitor commands
Type ```?|help|-?|--help``` to list commands.
```
> ?
Usage:  [command]

Commands:
  b      Breakpoints
  d      Disassembles 6502 code from emulator memory.
  f      Fill memory at specified address with a list of bytes.
         Example: f 1000 20 ff ab 30
  g      Change the PC (Program Counter) to the specified address continue execution.
  l      Load 6502 binary file from file pick dialog into emulator memory.
  ll     Load specified 6502 binary file into emulator memory.
  m      Show contents of emulator memory in bytes.
  q      Quit monitor.
  r      Show processor status and registers. CY = #cycles executed.
  reset  Resets the computer (soft, memory intact).
  s      Save a binary from 6502 emulator memory to host file system.
  z      Single step through instructions. Optionally execute a specified number of instructions.
```

Type ```[command] -?|--help``` to list help on specific command.

Example on help for ```d``` (disassemble) command:
```
> d -?
Usage:  d [options] <start> <end>

Arguments:
  start         Start address (hex). If not specified, the current PC address is used.
  end           End address (hex). If not specified, a default number of addresses will be shown from start.

Options:
  -?|-h|--help  Show help information.
```

Example how to load binary with ```l``` command:

_The machine code binary simple.prg adds two number from memory, divides by 2, stores it in another memory location_
```
> l C:\Source\dotnet-6502\.cache\Example\ConsoleTestPrograms\AssemblerSource\simple.prg
File loaded at 0xC000
```

Example how to disassemble with ```d``` command:

_Shows what the code in simple.prg does_
```
> d c000 c010
c000  ad 00 d0  LDA $D000
c003  18        CLC
c004  6d 01 d0  ADC $D001
c007  6a        ROR A
c008  8d 02 d0  STA $D002
c00b  00        BRK
c00c  00        BRK
c00d  00        BRK
c00e  00        BRK
c00f  00        BRK
c010  00        BRK
```

Example how to fill bytes in memory with ```f``` command:

_Sets value A and B in memory locations (d000 and d001) that simple.prg uses_
```
> f d000 12 30
```

Example how to set PC (Program Counter) with ```r pc``` command:

_Sets PC at load address of simple.prg_
```
> r pc c000
SP=00 PC=C000
```

Example how to execute  ```g``` command:

_Executes simple.prg, stops on BRK instruction_
```
> g c000
Will stop on BRK instruction.
Staring executing code at c000
Stopped at                0000
c00b  00        BRK
```

Example how to show contents of bytes in memory with ```m``` command:

_Inspects values A (d000), B (d001), and result (d002)_
```
> m d000 d002
d000  12 30 21
```

# Additional system-specific commands
- [C64](../Highbyte.DotNet6502.Systems/SYSTEMS_C64.md#monitor-commands)
