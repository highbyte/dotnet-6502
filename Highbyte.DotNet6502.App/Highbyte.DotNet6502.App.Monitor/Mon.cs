﻿using System;
using System.Diagnostics;
using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Systems.Generic;

namespace Highbyte.DotNet6502.App.Monitor
{
    public class Mon
    {
        public GenericComputer Computer { get; private set; }
        public CPU Cpu { get {return Computer.CPU;} }
        public Memory Mem { get {return Computer.Mem;} }
        public Mon()
        {
            var mem = new Memory();

            var computerBuilder = new GenericComputerBuilder();
            computerBuilder
                .WithCPU()
                //.WithStartAddress()
                .WithMemory(mem)
                .WithInstructionExecutedEventHandler( 
                    (s, e) => Debug.WriteLine(OutputGen.GetLastInstructionDisassembly(e.CPU, e.Mem)));
                // .WithExecOptions(options =>
                // {
                // });
            Computer = computerBuilder.Build();           
        }

        public void LoadBinary(string fileName, out ushort loadedAtAddress, ushort? forceLoadAddress = null)
        {
            BinaryLoader.Load(
                Mem,
                fileName,
                out loadedAtAddress,
                out int _,
                forceLoadAddress);
        }
    }
}
