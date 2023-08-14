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
```
dotnet add package Highbyte.DotNet6502 --prerelease
```

## Or compile .dll yourself
- Clone this repo ```git clone https://github.com/highbyte/dotnet-6502.git```
- Change dir to library ```cd dotnet-6502/src/libraries/Highbyte.DotNet6502```
- Build library ```dotnet build```
- In your app, add .dll reference to ```./bin/Debug/net7.0/Highbyte.DotNet6502.dll```

## Example of basic usage of Highbyte.DotNet6502 library

Example #1. Load compiled 6502 binary and execute it.
```c#
  var mem = BinaryLoader.Load(
      "C:\Binaries\MyCompiled6502Program.prg", 
      out ushort loadedAtAddress);
      
  var computerBuilder = new ComputerBuilder();
  computerBuilder
      .WithCPU()
      .WithStartAddress(loadedAtAddress)
      .WithMemory(mem);
      
  var computer = computerBuilder.Build();
  computer.Run();  
```  

Example #2. 6502 machine code for adding to numbers and dividing by 2
```c#
  // Test program 
  // - adds values from two memory location
  // - divides it by 2 (rotate right one bit position)
  // - stores it in another memory location

  // Load input data into memory
  byte value1 = 12;
  byte value2 = 30;
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

  // Initialize emulator with CPU, memory, and execution parameters
  var computerBuilder = new ComputerBuilder();
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
generates this output
``` console
C000  AD 00 D0  LDA $D000  
C003  18        CLC        
C004  6D 01 D0  ADC $D001  
C007  6A        ROR A      
C008  8D 02 D0  STA $D002  
C00B  00        BRK        
Execution stopped
CPU state: A=15 X=00 Y=00 PS=[-----I--] SP=FD PC=0000
Stats: 6 instruction(s) processed, and used 23 cycles.
Result: (12 + 30) / 2 = 21
```

## Model for bank switching
The 6502 CPU supports max 64KB of total memory (16 bit address space). To enable more memory to be used, a type of "bank switching" is supported in the memory implementation. X number of memory configurations can be created, and each populated with byte[] arrays for separate locations within the 64KB space. Switching between the different memory layouts is as easy as calling ```memory.SetMemoryConfiguration(x)```.

TODO: more details

