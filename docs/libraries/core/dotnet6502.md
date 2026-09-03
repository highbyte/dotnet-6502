# CPU

Library: `Highbyte.DotNet6502`

## Overview

- A stand-alone library for executing 6502 machine code programs.
- Has no UI, meant to be integrated into other applications.
- Emulation of 6502-family processors, selectable per CPU model (see below).
- Supports all official 6502 opcodes.
- Supports a compatibility-profile based subset of undocumented NMOS 6502 opcodes.
- Can load an assembled 6502 program binary and execute it.
- Passes this [Functional 6502 test program](https://github.com/Klaus2m5/6502_65C02_functional_tests).
- Every instruction performs exactly the bus accesses the silicon performs, one per clock cycle, verified against the [SingleStepTests 65x02](https://github.com/SingleStepTests/65x02) corpus (see below).

## Cycle and bus accuracy

An instruction executes atomically (one call runs it to completion), but every clock cycle of
it is a real memory access in hardware order, dummy reads and write-backs included: the
next-byte read of implied instructions, the un-indexed read of zero-page indexed modes, the
un-carried-address read of NMOS indexed modes on a page crossing, the branch fix-up reads, the
stack reads of pulls and returns, the NMOS read/write-back/write and 65C02 read/read/write of
read-modify-write instructions, and the 65C02's operand re-reads on its extra cycles. Memory-
mapped I/O therefore sees exactly the accesses a real device would, at the same points in the
sequence.

`CPU.BusCycles` counts those accesses. Because the count advanced by an instruction equals its
cycle count, systems can derive elapsed cycles from bus activity, and the tests hold the two
together for every opcode byte of every model and profile. A system can therefore keep its
devices exact to the cycle without stepping them every cycle: a memory-mapped register handler
reads the counter, advances the device to the cycle of the access, and then applies the access.
The C64 does this for the VIC-II, the CIAs and the SID.

A system can also stall the CPU the way a bus master holding RDY does, through
`CPU.BusStallSource`: before a read the CPU asks how many cycles the bus is busy, the read then
happens at the cycle the bus is released, and the waiting cycles count as instruction cycles
without accesses (so `BusCycles` is then the cycle count, of which the accesses are a subset).
Writes are never stalled, as on the 6510. The source names the next cycle at which it wants to be
asked again, so the check costs one comparison per read in between.

Interrupts follow the hardware sampling rule. The 6502 polls its IRQ and NMI inputs at the end
of an instruction's second-to-last cycle (a taken branch that does not cross a page polls at the
end of its first cycle), so a line that goes active during the last cycle is only seen after the
following instruction. A device reports the bus cycle on which it asserted the line, through the
`CPUInterrupts` overloads that take a cycle, and the CPU takes the interrupt at a boundary only if
that cycle is at or before the poll point. A source set active without a cycle is taken at the
next boundary. `CLI`, `SEI` and `PLP` change the I flag after the poll, so their effect on
interrupt recognition is one instruction late; `RTI` changes it in time. Not modelled: an NMI
hijacking an interrupt sequence already in progress.

Verification, beyond the functional test programs:

- A pinned subset of the SingleStepTests corpus, which records the exact bus cycles of the
  NMOS 6502 and the WDC 65C02, is run per opcode: final state, cycle-by-cycle bus trace and
  cycle count must all match. Bytes where the emulated part is documented to differ (the
  Rockwell bit instructions and WDC-only `WAI`/`STP` that the NCR 65C02 executes as NOPs, and
  the unstable NMOS opcodes no profile implements) are listed with their reason and skipped.
- A structural test executes every defined opcode byte of every model and profile from random
  state and requires one bus access per reported cycle.

What this does not claim: the CPU cannot be stopped inside an instruction, so a device that
needs to stall the CPU (the C64's VIC-II via BA/RDY) or sample a line on a specific cycle must
do so at the bus access for that cycle; and interrupts are recognized at instruction boundaries.

## CPU models

The CPU is constructed for a specific CPU model. Instruction identity is per model and
opcode byte — on a 65C02 the same byte can mean a different instruction than on an NMOS
6502, and the 65C02 adds instructions that do not exist on NMOS models.

| Model id | Display name | Notes |
| -------- | ------------ | ----- |
| `nmos6502` | NMOS 6502 | The default. Official instruction set plus profile-gated undocumented NMOS opcodes. |
| `mos6510` | MOS 6510 | Same instruction set as `nmos6502`, adds the on-chip I/O port at addresses $00/$01 (used by the C64). |
| `ncr65c02` | NCR 65C02 | CMOS 65C02 (base/NCR variant): new and redefined instructions, all 256 bytes defined, CMOS behavior differences. Supports only the `OfficialOnly` compatibility profile. |

Select a model via the `CPU` constructor overloads that take a `cpuModelId` string, e.g.
`new CPU(loggerFactory, CpuModelIds.Ncr65c02, CpuCompatibilityProfile.OfficialOnly)`.
The `CpuModelInfo` class provides model ids, display names, and supported profiles for
configuration UIs and validation.

Note: the public `OpCodeId` enum names the opcode bytes of the **NMOS instruction set
only** (models `nmos6502`/`mos6510`). It is a convenient vocabulary for writing NMOS
programs in code and for checking well-known official bytes (which are identical on all
models), but it is not model-aware: 65C02-only instructions have no enum member, and a
byte redefined on the 65C02 keeps its NMOS name in the enum. For model-correct per-byte
information use the model-aware members on `CPU` (`IsOpCodeDefined`, `GetOpCodeSize`) and
disassembly via `OutputGen`.

## Opcode compatibility profiles

The core CPU library exposes undocumented opcodes through `CpuCompatibilityProfile`.
Higher profiles include everything from lower profiles.

| Profile | Meaning |
| ------- | ------- |
| `OfficialOnly` | Only documented MOS 6502 opcodes are available. |
| `StableUnofficial` | Also enables the more predictable undocumented NMOS opcodes commonly used on real 6502/6510 hardware. |
| `ExperimentalUnofficial` | Also enables the currently implemented but less reliable undocumented opcodes used for targeted compatibility testing. |
| `FullUnofficial` | Also enables halt-style unofficial opcodes such as `JAM` / `KIL` that can intentionally jam the CPU until reset. |

Notes:

- `new CPU()` currently uses `ExperimentalUnofficial`.
- `GenericComputer` defaults to `ExperimentalUnofficial`.
- `C64` and `Vic20` default to `StableUnofficial`.
- These profiles describe the currently implemented undocumented **NMOS** opcode set. They are not a claim of complete coverage for every unofficial opcode found on every 6502-family variant.
- `JAM` / `KIL` behavior is modeled as the CPU entering a halted/jammed state until `Reset(...)` is called.

Example:

```c#
using Highbyte.DotNet6502;

var defaultCpu = new CPU();
var officialCpu = new CPU(CpuCompatibilityProfile.OfficialOnly);
var stableCpu = new CPU(CpuCompatibilityProfile.StableUnofficial);
var fullCpu = new CPU(CpuCompatibilityProfile.FullUnofficial);
```

## Requirements

See details under [Development](../../home/development.md).

## Using from a .NET application

### Reference NuGet package

```sh
dotnet add package Highbyte.DotNet6502 --prerelease
```

### Or compile .dll yourself

- Clone this repo: `git clone https://github.com/highbyte/dotnet-6502.git`
- Change dir to library: `cd dotnet-6502/src/libraries/Highbyte.DotNet6502`
- Build library: `dotnet build`
- In your app, add a `.dll` reference to `./bin/Debug/net10.0/Highbyte.DotNet6502.dll`

## Examples

### Example #1

#### Step 1 — Write a 6502 program in assembly

Write a 6502 assembly program to calculate the average of two values (from different memory locations) and store the result in a third memory location.

!!! note
    This example uses the [ACME](https://sourceforge.net/projects/acme-crossass/) cross-assembler syntax (builds exist for Windows and macOS, for Linux it requires downloading source code and building).
    There exist other 6502 cross [assemblers](http://www.6502.org/tools/asm/) that can be used (but which may have different syntax requirements).

Use a text editor (or IDE) to create a text file with the contents below and save it to `calc_avg.asm`.

!!! tip
    `VSCode` has an extension called [`VS64`](https://marketplace.visualstudio.com/items?itemName=rosc.vs64) that provides nice syntax highlighting for 6502 assembly code (`.asm`).

```
;Calculates the average of two values stored in memory locations, and stores the result in another memory location.
;Code written in 6502 assembler using ACME cross assembler syntax.

;code start address
* = $c000

;!to "./calc_avg.prg"
    lda $d000
    clc
    adc $d001
    ror
    sta $d002
;In emulator, setup hitting brk instruction to stop
    brk
```

#### Step 2 — Assemble program to binary .prg file

Example assumes ACME is installed, and the `acme` executable is in PATH.

PowerShell / Bash:

```sh
acme -f cbm -o calc_avg.prg calc_avg.asm

# or examples if acme is not in path: 
# & "$($env:USERPROFILE)\c64\acme\acme.exe" -f cbm -o calc_avg.prg calc_avg.asm
# ~/c64/acme/acme -f cbm -o calc_avg.prg calc_avg.asm
```

#### Step 3 (optional) — Inspect binary .prg file

!!! note
    If the binary was assembled with the `-f cbm` parameter (as in the example above), the two first bytes in the `.prg` file would be the load address specified in the source `.asm` file (`* = $c000`), in *little endian* order `00`,`C0`. This is usually the convention for Commodore computers, and convenient in other contexts also.
    If the binary was assembled with the `-f plain` parameter, the binary file would not have the first two address bytes, and only contain the code (and data) declared in the source file.

`PowerShell` (Windows, Linux, macOS):

```powershell
(Format-Hex ./calc_avg.prg).HexBytes
```

```
00 C0 AD 00 D0 18 6D 01 D0 6A 8D 02 D0 00
```

`Bash` (Linux) and `Zsh` (macOS):

```bash
hexdump -ve '1/1 "%.2x "' ./calc_avg.prg
```

```
00 c0 ad 00 d0 18 6d 01 d0 6a 8d 02 d0 00
```

#### Step 4 — Load compiled 6502 binary and execute it

A .NET C# console program that runs the 6502 program.

`Program.cs`:

```c#
// ----------------------------------------------------------------------------------------------------
// A minimal example of how to load and run a 6502 machine code program.
// This does not involve a complete computer (such as Commodore 64) but only the CPU and memory.
// ----------------------------------------------------------------------------------------------------

using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Utils;

string programFile = "calc_avg.prg";

// Create memory (default 64KB) and load the machine code program into it. Assume two first bytes in the .prg file is the load address.
var mem = BinaryLoader.Load(programFile, out ushort loadAddress);

// Init variables in memory locations used by the program.
mem[0xd000] = 64;
mem[0xd001] = 20;
Console.WriteLine($"Input 1 (0xd000) = {mem[0xd000]}");
Console.WriteLine($"Input 2 (0xd001) = {mem[0xd001]}");

// Create the CPU and set program counter (start address).
var cpu = new CPU();
cpu.PC = loadAddress;

// Run program. The 6502 program will run until a BRK instruction is encountered.
cpu.Execute(mem, LegacyExecEvaluator.UntilBRKExecEvaluator);

// Inspect result of program which is stored in memory location 0xd002.
Console.WriteLine($"Output  (0xd002) = {mem[0xd002]}");
```

Result:

```
Input 1 (0xd000) = 64
Input 2 (0xd001) = 20
Output  (0xd002) = 42
```

### Example #2 — Enter 6502 machine code directly and show processor status

`Program.cs`:

```c#
// ----------------------------------------------------------------------------------------------------
// An example of how to enter a machine code program directly into memory,
// and instantiating the "Generic" computer with logging of executed instructions.
// ----------------------------------------------------------------------------------------------------

using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Utils;

// Test program to calculate average of two values
// - adds values from two memory locations
// - divides it by 2 (rotate right one bit position)
// - stores it in another memory location

// Load input data into memory
byte value1 = 64;
byte value2 = 20;
ushort value1Address = 0xd000;
ushort value2Address = 0xd001;
ushort resultAddress = 0xd002;
var mem = new Memory();
mem[value1Address] = value1;
mem[value2Address] = value2;

// Load machine code into memory
ushort codeAddress = 0xc000;
ushort codeInsAddress = codeAddress;
mem[codeInsAddress++] = 0xad;         // LDA (Load Accumulator)
mem[codeInsAddress++] = 0x00;         //  |-Lowbyte of $d000
mem[codeInsAddress++] = 0xd0;         //  |-Highbyte of $d000
mem[codeInsAddress++] = 0x18;         // CLC (Clear Carry flag)
mem[codeInsAddress++] = 0x6d;         // ADC (Add with Carry, adds memory to accumulator)
mem[codeInsAddress++] = 0x01;         //  |-Lowbyte of $d001
mem[codeInsAddress++] = 0xd0;         //  |-Highbyte of $d001
mem[codeInsAddress++] = 0x6a;         // ROR (Rotate Right, rotates accumulator right one bit position)
mem[codeInsAddress++] = 0x8d;         // STA (Store Accumulator, store accumulator to memory)
mem[codeInsAddress++] = 0x02;         //  |-Lowbyte of $d002
mem[codeInsAddress++] = 0xd0;         //  |-Highbyte of $d002
mem[codeInsAddress++] = 0x00;         // BRK (Break/Force Interrupt) - emulator configured to stop execution when reaching this instruction

// Initialize a "Generic" 6502 computer emulator with CPU, memory, and execution parameters
var computerBuilder = new GenericComputerBuilder();
computerBuilder
    .WithCPU()
    .WithStartAddress(codeAddress)
    .WithMemory(mem)
    .WithInstructionExecutedEventHandler(
        (s, e) => Console.WriteLine(OutputGen.GetLastInstructionDisassembly(e.CPU, e.Mem)))
    .WithExecOptions(options =>
    {
        options.ExecuteUntilInstruction = OpCodeId.BRK; // Emulator will stop executing when a BRK instruction is reached.
    });
var computer = computerBuilder.Build();

// Run program
computer.Run();
Console.WriteLine($"Execution stopped");
Console.WriteLine($"CPU state: {OutputGen.GetProcessorState(computer.CPU)}");
Console.WriteLine($"Stats: {computer.CPU.ExecState.InstructionsExecutionCount} instruction(s) processed, and used {computer.CPU.ExecState.CyclesConsumed} cycles.");

// Print result
byte result = mem[resultAddress];
Console.WriteLine($"Result: ({value1} + {value2}) / 2 = {result}");
```

Result:

```
C000  AD 00 D0  LDA $D000  
C003  18        CLC        
C004  6D 01 D0  ADC $D001  
C007  6A        ROR A      
C008  8D 02 D0  STA $D002  
C00B  00        BRK        
Execution stopped
CPU state: A=15 X=00 Y=00 PS=[-----I--] SP=FD PC=0000
Stats: 6 instruction(s) processed, and used 23 cycles.
Result: (64 + 20) / 2 = 41
```

## Model for bank switching

The 6502 CPU supports max 64KB of total memory (16-bit address space). To enable more memory to be used, a type of "bank switching" is supported in the memory implementation. X number of memory configurations can be created, and each populated with byte[] arrays for separate locations within the 64KB space.

The `Memory` constructor parameter `numberOfConfigurations` (default 1) specifies how many memory configurations to support:

```c#
var mem = new Memory(numberOfConfigurations: 4);
```

Switch between the different memory configurations by calling `SetMemoryConfiguration`:

```c#
mem.SetMemoryConfiguration(2)
```

TODO: more details
