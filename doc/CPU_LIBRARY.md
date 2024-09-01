<h1 align="center">Highbyte.DotNet6502 library</h1>

# Overview
- A stand-alone library for executing 6502 machine code programs.
- Has no UI, meant to be integrated in other applications.
- Emulation of a 6502 processor
- Supports all official 6502 opcodes
- Can load an assembled 6502 program binary and execute it
- Passes this [Functional 6502 test program](https://github.com/Klaus2m5/6502_65C02_functional_tests)

# Requirements
- See details [here](DEVELOP.md#Requirements)

# Using from a .NET application
## Reference NuGet package
```shell
dotnet add package Highbyte.DotNet6502 --prerelease
```

## Or compile .dll yourself
- Clone this repo `git clone https://github.com/highbyte/dotnet-6502.git`
- Change dir to library `cd dotnet-6502/src/libraries/Highbyte.DotNet6502`
- Build library `dotnet build`
- In your app, add .dll reference to `./bin/Debug/net8.0/Highbyte.DotNet6502.dll`

## Example of basic usage of Highbyte.DotNet6502 library

## Example #1. 

### Step 1 - Write a 6502 program in assembly
Write a 6502 assembly program to calculate average of two values (from different memory locations) and store result in a third memory location.

> [!INFORMATION]
> This example uses the [ACME](https://sourceforge.net/projects/acme-crossass/) cross assembler syntax (builds exists for Windows and macOS, for Linux it requires to download source code and build).
> There exists other 6502 cross [assemblers](http://www.6502.org/tools/asm/) that can be used (but which may have different syntax requirements).

Use a text editor (or IDE) to create a text file with the contents below and save it to `calc_avg.asm`.
> [!TIP]
> `VSCode` has an extension called [`VS64`](https://marketplace.visualstudio.com/items?itemName=rosc.vs64) that provides nice syntax highlighting for 6502 assembly code (.asm).

```asm       
;Calculates the average of two values stored in memory locations, and store the result in another memory location.
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

### Step 2 - Assemble program to binary .prg file.
Example assumes ACME is installed, and acme executable is in path.

PowerShell/Bash
```shell
acme -f cbm -o calc_avg.prg calc_avg.asm

# or examples if acme is not in path: 
# & "$($env:USERPROFILE)\c64\acme\acme.exe" -f cbm -o calc_avg.prg calc_avg.asm
# ~/c64/acme/acme -f cbm -o calc_avg.prg calc_avg.asm
```

### Step 3 (optional) - Inspect binary .prg file

> [!NOTE]
> If the binary was assembled with the `-f cbm` parameter (as in the example above), the two first bytes in the .prg file would be the load address specified in the source .asm file (`* = $c000`), in "little endian order" `00`,`C0`. This is usually the convention for Commodore computers, and convenient in other contexts also.
> If the binary was assembled with the `-f plain` parameter, the binary file would not have the first two address bytes, and only contain the code (and data) declared in the source file.

`PowerShell` (Windows, Linux, macOS)
```powershell
(Format-Hex ./calc_avg.prg).HexBytes
```
```
AD 00 D0 18 6D 01 D0 6A 8D 02 D0 00
```

`Bash` (Linux) and `Zsh` (macOS)
```bash
hexdump -ve '1/1 "%.2x "' ./calc_avg.prg
```
```
ad 00 d0 18 6d 01 d0 6a 8d 02 d0 00
```

### Step 4 - Load compiled 6502 binary and execute it.

DotNet C# console program that runs the 6502 program.

`Program.cs`
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

Result
```
Input 1 (0xd000) = 64
Input 2 (0xd001) = 20
Output  (0xd002) = 42
```


## Example #2. Enter 6502 machine code directly and show processor status

`Program.cs`
```c#
// ----------------------------------------------------------------------------------------------------
// An example of how to enter a machine code program directly into memory,
// and instanciating the "Generic" computer with logging of executed instructions.
// ----------------------------------------------------------------------------------------------------

using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Utils;

// Test program to calculate average of two values
// - adds values from two memory location
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
mem[codeInsAddress++] = 0x8d;         // STA (Store Accumulator, store to accumulator to memory)
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
Result
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
The 6502 CPU supports max 64KB of total memory (16 bit address space). To enable more memory to be used, a type of "bank switching" is supported in the memory implementation. X number of memory configurations can be created, and each populated with byte[] arrays for separate locations within the 64KB space. 

The `Memory` constructor parameter `numberOfConfigurations` (default 1) specifies how many memory configuration to support:

```c#
var mem = new Memory(numberOfConfigurations: 4);
```
Switch between the different memory configurations by calling `SetMemoryConfiguration`:

```c#
mem.SetMemoryConfiguration(2)
```

TODO: more details

