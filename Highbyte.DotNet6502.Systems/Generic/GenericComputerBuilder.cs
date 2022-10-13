using System;
using System.Diagnostics;
using System.IO;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Systems.Generic
{

    public class GenericComputerBuilder
    {
        private readonly GenericComputer _genericComputer;

        public GenericComputerBuilder()
        {
            var emulatorScreenConfig = new EmulatorScreenConfig();
            _genericComputer = new GenericComputer(emulatorScreenConfig);
        }

        public GenericComputerBuilder(EmulatorScreenConfig emulatorScreenConfig)
        {
            _genericComputer = new GenericComputer(emulatorScreenConfig);
        }

        public GenericComputerBuilder WithCPU()
        {
            _genericComputer.CPU = new CPU();
            return this;
        }

        public GenericComputerBuilder WithCPU(CPU cpu)
        {
            _genericComputer.CPU = cpu;
            return this;
        }

        public GenericComputerBuilder WithStartAddress(ushort startAddress)
        {
            _genericComputer.CPU.PC = startAddress;
            return this;
        }

        public GenericComputerBuilder WithMemory(
            int memorySize = 1024 * 64
            )
        {
            var mem = new Memory(memorySize, mapToDefaultRAM: true);
            return WithMemory(mem);
        }

        public GenericComputerBuilder WithMemory(
            Memory mem
            )
        {
            _genericComputer.Mem = mem;
            return this;
        }

        public GenericComputerBuilder WithExecOptions(Action<ExecOptions> configure)
        {
            var newExecOptions = new ExecOptions();
            configure(newExecOptions);
            _genericComputer.DefaultExecOptions = newExecOptions;
            return this;
        }

        public GenericComputerBuilder WithInstructionToBeExecutedEventHandler(EventHandler<CPUInstructionToBeExecutedEventArgs> onInstructionToBeExecuted)
        {
            _genericComputer.CPU.InstructionToBeExecuted += onInstructionToBeExecuted;
            return this;
        }
        public GenericComputerBuilder WithInstructionExecutedEventHandler(EventHandler<CPUInstructionExecutedEventArgs> onInstructionExecuted)
        {
            _genericComputer.CPU.InstructionExecuted += onInstructionExecuted;
            return this;
        }

        public GenericComputerBuilder WithUnknownInstructionEventHandler(EventHandler<CPUUnknownOpCodeDetectedEventArgs> onUnknownOpCodeDetected)
        {
            _genericComputer.CPU.UnknownOpCodeDetected += onUnknownOpCodeDetected;
            return this;
        }

        public GenericComputer Build()
        {
            return _genericComputer;
        }

        public static GenericComputer SetupGenericComputerFromConfig(GenericComputerConfig emulatorConfig)
        {
            emulatorConfig.Validate();

            Debug.WriteLine($"Loading 6502 machine code binary file.");
            Debug.WriteLine($"{emulatorConfig.ProgramBinaryFile}");
            if(!File.Exists(emulatorConfig.ProgramBinaryFile))
            {
                Debug.WriteLine($"File does not exist.");
                throw new Exception($"Cannot find 6502 binary file: {emulatorConfig.ProgramBinaryFile}");
            }

            var mem = new Memory();

            BinaryLoader.Load(
                mem,
                emulatorConfig.ProgramBinaryFile,
                out ushort loadedAtAddress,
                out ushort fileLength);

            // Initialize emulator with CPU, memory, and execution parameters
            var computerBuilder = new GenericComputerBuilder(emulatorConfig.Memory.Screen);
            computerBuilder
                .WithCPU()
                .WithStartAddress(loadedAtAddress)
                .WithMemory(mem)
                .WithExecOptions(options =>
                {
                    // Emulator will stop executing when a BRK instruction is reached.
                    options.ExecuteUntilInstruction = emulatorConfig.StopAtBRK ? OpCodeId.BRK : null;
                });

            var genericComputer = computerBuilder.Build();
            return genericComputer;
        }
    }
}