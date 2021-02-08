using System;
using System.Collections.Generic;
using System.IO;

namespace Highbyte.DotNet6502.ConsoleUI
{
    public class RunTestProgram
    {
        public static void Run()
        {
            Console.WriteLine($"-----------------------------------------------------");
            Console.WriteLine($"Run 6502 code that copies data between two addresses.");
            Console.WriteLine($"-----------------------------------------------------");

            string prgFileName = "../.cache/Highbyte.DotNet6502.ConsoleUI/testprogram.prg";
            Console.WriteLine($"Program binary file: {prgFileName}");
            if(!File.Exists(prgFileName))
            {
                Console.WriteLine($"File does not exist.");
                return;
            }

            // Load binary file
            byte[] fileData = File.ReadAllBytes(prgFileName);
            // First two bytes of binary file is assumed to be start address, little endian notation.
            ushort startPos = ByteHelpers.ToLittleEndianWord(fileData[0], fileData[1]);
            // The rest of the bytes are considered the code
            byte[] code = new byte[fileData.Length-2];
            Array.Copy(fileData, 2, code, 0, fileData.Length-2);

            Console.WriteLine($"Code memory address: {startPos:X4}    / {startPos}");
            Console.WriteLine($"Code length (bytes): {code.Length:X4} / {code.Length}");


            Console.WriteLine("Creating memory...");
            Memory mem = new();

            ushort testDataAddress = 0x1000;
            Console.WriteLine($"Loading test-data into memory address: {testDataAddress:X4}");
            mem.WriteByte(ref testDataAddress, 0x01);
            mem.WriteByte(ref testDataAddress, 0x02);
            mem.WriteByte(ref testDataAddress, 0x03);
            testDataAddress = 0x10fe;
            mem.WriteByte(ref testDataAddress, 0xa3);
            mem.WriteByte(ref testDataAddress, 0xa2);
            mem.WriteByte(ref testDataAddress, 0xa1);

            //Console.WriteLine("Press Enter to start");
            //Console.ReadLine();

            // Load code in to memory at start position
            Console.WriteLine("Loading code into memory...");
            mem.StoreData(startPos, code);

            // Initialize CPU, set PC to start position
            Console.WriteLine("Initializing CPU...");
            CPU cpu = new();
            cpu.PC = startPos;

            var execOptions = new ExecOptions
            {
                UnknownInstructionThrowsException = true,
                ExecuteUntilInstruction = new List<byte>{Ins.BRK.ToByte()}
            };

            // Execute program
            Console.WriteLine("Executing code...");
            //var consumedCycles = cpu.Execute(mem, 10000, maxNumberOfInstructions: 1 + (4*256));
            var consumedCycles = cpu.Execute(mem, execOptions);

            Console.WriteLine("Program ended.");
            Console.WriteLine($"Consumed cycles: {consumedCycles}");

            ushort verifySrcAddress = 0x1000;
            ushort verifyDestAddress = 0x2000;
            Console.WriteLine($"Comparing data in source {verifySrcAddress:X4} with destination {verifyDestAddress:X4}");
            Console.WriteLine($"{verifySrcAddress:X4} = {mem.FetchByte(ref verifySrcAddress):X2}, {verifyDestAddress:X4} = {mem.FetchByte(ref verifyDestAddress):X2}  ");
            Console.WriteLine($"{verifySrcAddress:X4} = {mem.FetchByte(ref verifySrcAddress):X2}, {verifyDestAddress:X4} = {mem.FetchByte(ref verifyDestAddress):X2}  ");
            Console.WriteLine($"{verifySrcAddress:X4} = {mem.FetchByte(ref verifySrcAddress):X2}, {verifyDestAddress:X4} = {mem.FetchByte(ref verifyDestAddress):X2}  ");

            verifySrcAddress = 0x10fe;
            verifyDestAddress = 0x20fe;
            Console.WriteLine($"Comparing data in source {verifySrcAddress:X4} with destination {verifyDestAddress:X4}");
            Console.WriteLine($"{verifySrcAddress:X4} = {mem.FetchByte(ref verifySrcAddress):X2}, {verifyDestAddress:X4} = {mem.FetchByte(ref verifyDestAddress):X2}  ");
            Console.WriteLine($"{verifySrcAddress:X4} = {mem.FetchByte(ref verifySrcAddress):X2}, {verifyDestAddress:X4} = {mem.FetchByte(ref verifyDestAddress):X2}  ");
            Console.WriteLine($"{verifySrcAddress:X4} = {mem.FetchByte(ref verifySrcAddress):X2}, {verifyDestAddress:X4} = {mem.FetchByte(ref verifyDestAddress):X2}  ");

        }
    }
}
