using System;
using System.Collections.Generic;
using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Systems.Generic;

namespace ConsoleTestPrograms
{
    public class RunSimple
    {
        public static void Run()
        {
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
        }
    }
}
