# Monitor

Library: `Highbyte.DotNet6502.Monitor`

## Overview

A library that provides a base for common built-in machine code monitor functionality for the [`Highbyte.DotNet6502`](dotnet6502.md) CPU library.

The library needs to be implemented for a specific UI technology — see the different host applications under [Desktop apps](../../desktop-apps/overview.md) and [Web apps](../../web-apps/overview.md).

Specific emulated *Systems* (computers) can also implement additional commands that are specific to that system — for example, see [C64 monitor commands](../../systems/c64/overview.md#monitor-commands).

!!! note
    There is also the [VS Code debugger extension](../../tools/vscode-debugger/debugging.md) that can be used for debugging both assembly source and raw disassembly. It's a lot more powerful than the built-in machine code monitor described here.

## General monitor commands

Type `?|help|-?|--help` to list commands.

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

Type `[command] -?|--help` to list help on a specific command.

Example help for `d` (disassemble) command:

```
> d -?
Usage:  d [options] <start> <end>

Arguments:
  start         Start address (hex). If not specified, the current PC address is used.
  end           End address (hex). If not specified, a default number of addresses will be shown from start.

Options:
  -?|-h|--help  Show help information.
```

Example how to load a binary with `l` command:

*The machine code binary `simple.prg` adds two numbers from memory, divides by 2, stores it in another memory location.*

```
> l C:\Source\dotnet-6502\samples\Assembler\Generic\Build\simple.prg
File loaded at 0xC000
```

Example how to disassemble with `d` command:

*Shows what the code in `simple.prg` does:*

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

Example how to fill bytes in memory with `f` command:

*Sets value A and B in memory locations (`d000` and `d001`) that `simple.prg` uses:*

```
> f d000 12 30
```

Example how to set PC (Program Counter) with `r pc` command:

*Sets PC to load address of `simple.prg`:*

```
> r pc c000
SP=00 PC=C000
```

Example how to execute `g` command:

*Executes `simple.prg`, stops on BRK instruction. Note that PC has been changed to IRQ vector, which in this example is at `0000`:*

```
> g c000
BRK instruction at c00b triggered stop.
0000  00        BRK
```

Example how to show contents of bytes in memory with `m` command:

*Inspects values A (`d000`), B (`d001`), and result (`d002`):*

```
> m d000 d002
d000  12 30 21
```

## Additional system-specific commands

- [C64 monitor commands](../../systems/c64/overview.md#monitor-commands)
