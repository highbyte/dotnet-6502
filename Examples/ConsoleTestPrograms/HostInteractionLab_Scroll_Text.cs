 using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Systems.Generic;

namespace ConsoleTestPrograms
{
    public class HostInteractionLab_Scroll_Text
    {
        public static void Run()
        {
            Console.Clear();

            string prgFileName = "../../../../../.cache/Examples/Assembler/Generic/hostinteraction_scroll_text.prg";
            Console.WriteLine($"Loading 6502 machine code binary file.");
            Console.WriteLine($"{prgFileName}");
            if(!File.Exists(prgFileName))
            {
                Console.WriteLine($"File does not exist.");
                return;
            }

            var mem = BinaryLoader.Load(
                prgFileName, 
                out ushort loadedAtAddress, 
                out ushort fileLength);

            // Initialize emulator with CPU, memory, and execution parameters
            var computerBuilder = new GenericComputerBuilder();
            computerBuilder
                .WithCPU()
                .WithStartAddress(loadedAtAddress)
                .WithMemory(mem)
                // .WithInstructionExecutedEventHandler( 
                //     (s, e) => Console.WriteLine(OutputGen.GetLastInstructionDisassembly(e.CPU, e.Mem)))
                .WithExecOptions(options =>
                {
                    options.ExecuteUntilInstruction = OpCodeId.BRK; // Emulator will stop executing when a BRK instruction is reached.
                });
            var computer = computerBuilder.Build();

            // The shared memory location in the emulator that the 6502 program writes to to update screen.
            // 80 columns and 25 rows, 1 byte per character = 2000 (0x07d0) bytes
            const ushort SCREEN_MEM             = 0x1000;
            const int SCREEN_MEM_COLS           = 80;
            // const int SCREEN_MEM_ROWS           = 25;
            const ushort SCREEN_REFRESH_STATUS  = 0xf000;
            const int SCREEN_REFRESH_STATUS_HOST_REFRESH_BIT = 0;
            const int SCREEN_REFRESH_STATUS_EMULATOR_DONE_BIT = 1;


            Console.WriteLine("");
            Console.WriteLine("Screen being updated indirectly by 6502 machine code running emulator!");
            Console.WriteLine("");

            bool cont =true;
            while(cont)
            {
                // Set emulator Refresh bit (maybe controlled by host frame counter in future?)
                // Emulator will wait until this bit is set until "redrawing" new data into memory
                mem.SetBit(SCREEN_REFRESH_STATUS, SCREEN_REFRESH_STATUS_HOST_REFRESH_BIT);

                bool shouldExecuteEmulator = true;
                while(shouldExecuteEmulator)
                {
                    // Execute a number of instructions
                    computer.Run(LegacyExecEvaluator.InstructionCountExecEvaluator(10));
                    shouldExecuteEmulator = !mem.IsBitSet(SCREEN_REFRESH_STATUS, SCREEN_REFRESH_STATUS_EMULATOR_DONE_BIT);
                }

                RenderRow(mem, SCREEN_MEM, SCREEN_MEM_COLS);
                //RenderScreen(mem, SCREEN_MEM, SCREEN_MEM_COLS, SCREEN_MEM_ROWS);

                // Clear the flag that the emulator set to indicate it's done.
                mem.ClearBit(SCREEN_REFRESH_STATUS, SCREEN_REFRESH_STATUS_EMULATOR_DONE_BIT);

                bool shouldExecuteHost = true;
                while(shouldExecuteHost)
                {
                    Thread.Sleep(80);  // Control speed of screen update
                    shouldExecuteHost = false;
                }

            } 

            Console.WriteLine($"Execution stopped");
            Console.WriteLine($"CPU state: {OutputGen.GetProcessorState(computer.CPU)}");
            Console.WriteLine($"Stats: {computer.CPU.ExecState.InstructionsExecutionCount} instruction(s) processed, and used {computer.CPU.ExecState.CyclesConsumed} cycles.");
        }

        private static void RenderRow(Memory mem, ushort screenMemAddress, int screenCols)
        {
            // Build screen data characters based on emulator memory contents (byte)
            ushort currentAddress = screenMemAddress;
            List<string> rows = new();
            string row="";
            for (int x = 0; x < screenCols; x++)
            {
                byte charByte = mem[currentAddress++];;
                char character = Convert.ToChar(charByte);
                row+= character;
            }
            rows.Add(row);

            // Write screen data from 0,0 position
            const int ROW_OFFSET_HOST = 5;
            Console.SetCursorPosition(0, 0 + ROW_OFFSET_HOST);
            Console.WriteLine(row);
        }

        // private static void RenderScreen(Memory mem, ushort screenMemAddress, int screenCols, int screenRows)
        // {
        //     // Build screen data characters based on emulator memory contents (byte)
        //     ushort currentAddress = screenMemAddress;
        //     List<string> rows = new();
        //     for (int y = 0; y < screenRows; y++)
        //     {
        //         string row="";
        //         for (int x = 0; x < screenCols; x++)
        //         {
        //             byte charByte = mem[currentAddress++];;
        //             char character = Convert.ToChar(charByte);
        //             row+= character;
        //         }
        //         rows.Add(row);
        //     }

        //     // Write screen data from 0,0 position
        //     Console.SetCursorPosition(0, 0);
        //     foreach(var row in rows)
        //     {
        //         Console.WriteLine(row);
        //     }            
        // }
    }
}
