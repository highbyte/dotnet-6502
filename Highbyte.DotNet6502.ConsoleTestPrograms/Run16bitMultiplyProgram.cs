using System;
using System.Collections.Generic;

namespace Highbyte.DotNet6502.ConsoleTestPrograms
{
    public class Run16bitMultiplyProgram
    {
        public static void Run()
        {
            Console.WriteLine($"--------------------------------------------------------");
            Console.WriteLine($"Run 6502 code that multiplies two 16 bit signed numbers.");
            Console.WriteLine($"--------------------------------------------------------");

            string prgFileName = "../.cache/Highbyte.DotNet6502.ConsoleTestPrograms/AssemblerSource/multiply_2_16bit_numbers.prg";

            Console.WriteLine("");
            Console.WriteLine($"Loading binary into emulator memory...");

            var mem = BinaryLoader.Load(
                prgFileName, 
                out ushort loadedAtAddress);

            Console.WriteLine($"Loading done.");
            Console.WriteLine("");
            Console.WriteLine($"Data & code load address:  {loadedAtAddress.ToHex(), 10} ({loadedAtAddress})");
            Console.WriteLine($"Code start address:        {loadedAtAddress.ToHex(), 10} ({loadedAtAddress})");

            ushort valA           = 1337;
            ushort valB           = 42;
            Console.WriteLine("");
            Console.WriteLine($"Multiply {valA.ToDecimalAndHex()} with {valB.ToDecimalAndHex()}");

            ushort sourceAddressA = 0xd000;
            ushort sourceAddressB = 0xd002;
            ushort resultAddress  = 0xd004;
            mem[(ushort)(sourceAddressA + 0)] = valA.Lowbyte();
            mem[(ushort)(sourceAddressA + 1)] = valA.Highbyte();
            mem[(ushort)(sourceAddressB + 0)] = valB.Lowbyte();
            mem[(ushort)(sourceAddressB + 1)] = valB.Highbyte();
            Console.WriteLine("");
            Console.WriteLine($"Value A set memory location {sourceAddressA.ToHex()}");
            Console.WriteLine($"{((ushort)(sourceAddressA + 0)).ToHex()} : {mem[(ushort)(sourceAddressA + 0)].ToHex()}");
            Console.WriteLine($"{((ushort)(sourceAddressA + 1)).ToHex()} : {mem[(ushort)(sourceAddressA + 1)].ToHex()}");
            Console.WriteLine($"Value B set memory location {sourceAddressB.ToHex()}");
            Console.WriteLine($"{((ushort)(sourceAddressB + 0)).ToHex()} : {mem[(ushort)(sourceAddressB + 0)].ToHex()}");
            Console.WriteLine($"{((ushort)(sourceAddressB + 1)).ToHex()} : {mem[(ushort)(sourceAddressB + 1)].ToHex()}");

            // Initialize CPU, set PC to start position
            var computerBuilder = new ComputerBuilder();
            computerBuilder
                .WithCPU()
                .WithStartAddress(loadedAtAddress)
                .WithMemory(mem)
                .WithInstructionExecutedEventHandler( 
                    (s, e) => Console.WriteLine($"{e.CPU.PC.ToHex()}: {e.CPU.ExecState.LastOpCode.Value.ToOpCodeId()}"))
                .WithExecOptions(options =>
                {
                    options.ExecuteUntilInstruction = new List<byte>{OpCodeId.BRK.ToByte()};
                });

            var computer = computerBuilder.Build();

            Console.WriteLine("");
            Console.WriteLine("Running 6502 multiplication routine...");
            computer.Run();

            Console.WriteLine("");
            Console.WriteLine("Done.");
            Console.WriteLine("");
            Console.WriteLine($"Result stored at {resultAddress.ToHex()}:");
            Console.WriteLine($"{((ushort)(resultAddress + 0)).ToHex()} : {mem[(ushort)(resultAddress + 0)].ToHex()}");
            Console.WriteLine($"{((ushort)(resultAddress + 1)).ToHex()} : {mem[(ushort)(resultAddress + 1)].ToHex()}");

            Console.WriteLine("");
            ushort result = mem.FetchWord(resultAddress);
            Console.WriteLine($"Result:");
            Console.WriteLine($"{valA.ToDecimalAndHex()} * {valB.ToDecimalAndHex()} = {result.ToDecimalAndHex()}");
        }
    }
}
