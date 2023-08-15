// using System.Diagnostics;
// using Xunit;

// namespace Highbyte.DotNet6502.Tests
// {
//     public class MemorySegmentBankSwitchingCPUTest
//     {
//         [Fact]
//         public void Can_Change_Segment_Memory_Bank_By_Setting_Special_Memory_Location()
//         {
//             // Arrange
//             var mem = new Memory(enableBankSwitching: true);

//             // Add a new bank to segment 1 with blank RAM. Segment 1 starts at 8192 (0x2000).
//             // Adding a new bank does not change current bank number (which will still be 0)
//             mem.AddMemorySegmentBank(1);   

//             // Fill some data in to segment 1 in current bank number (0).
//             mem[0x2000] = 0x42;
//             mem[0x2001] = 0x21;

//             // Load machine code into memory that will switch segment 1 bank from 0 to 1
//             ushort codeAddress = 0xc000;
//             ushort codeInsAddress = codeAddress;
//             // Prepare memory address 0x02 with memory segment bank number to use. Writing to 0x02 will not actually do the change.
//             mem[codeInsAddress++] = (byte)OpCodeId.LDA_I;   // LDA (Load Accumulator)
//             mem[codeInsAddress++] = 0x01;                   //  |-Value: The memory segment bank number to put in actual memory
//             mem[codeInsAddress++] = (byte)OpCodeId.STA_ZP;   // STA (Store Accumulator)
//             mem[codeInsAddress++] = 0x02;                   //  |-ZeroPage address $0002
//             // Write the segment number to address 0x01, which will trigger the bank number specified in 0x01 to be loaded in to the segment number written to 0x01.
//             mem[codeInsAddress++] = (byte)OpCodeId.LDA_I;   // LDA (Load Accumulator)
//             mem[codeInsAddress++] = 0x01;                   //  |-Value: The memory segment number to change.
//             mem[codeInsAddress++] = (byte)OpCodeId.STA_ZP;   // STA (Store Accumulator)
//             mem[codeInsAddress++] = 0x01;                   //  |-ZeroPage address $0001
//             mem[codeInsAddress++] = 0x00;                   // BRK (Break/Force Interrupt) - emulator configured to stop execution when reaching this instruction

//             // Initialize emulator with CPU, memory, and execution parameters
//             var computerBuilder = new ComputerBuilder();
//             computerBuilder
//                 .WithCPU()
//                 .WithStartAddress(codeAddress)
//                 .WithMemory(mem)
//                 .WithInstructionExecutedEventHandler( 
//                     (s, e) => Debug.WriteLine(OutputGen.GetLastInstructionDisassembly(e.CPU, e.Mem)))
//                 .WithExecOptions(options =>
//                 {
//                     options.ExecuteUntilInstruction = OpCodeId.BRK; // Emulator will stop executing when a BRK instruction is reached.
//                 });
//             var computer = computerBuilder.Build();

//             // Act
//             computer.Run();

//             // Assert
//             // Check that segment 1 now has changed bank number to 1.
//             Assert.Equal(1, mem.MemorySegments[1].CurrentBankNumber);
//             // Check that content of the memory in segment 1 now is blank (bank 1 was blank when we added it)
//             Assert.Equal(0x00, mem[0x2000]);
//             Assert.Equal(0x00, mem[0x2001]);
//         }

//     }
// }
