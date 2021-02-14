using System;

namespace Highbyte.DotNet6502
{

    public class ComputerBuilder
    {
        private readonly Computer _computer;
        public ComputerBuilder()
        {
            _computer = new Computer();
        }

        public ComputerBuilder NewComputerBuilder() => new();

        public ComputerBuilder WithCPU()
        {
            _computer.CPU = new CPU();
            return this;
        }
        

        public ComputerBuilder WithStartAddress(ushort startAddress)
        {
            _computer.CPU.PC = startAddress;
            return this;
        }
        
        public ComputerBuilder WithMemory(
            uint memorySize = 1024*64
            )
        {
            _computer.Mem = new Memory(memorySize);
            return this;
        }
        public ComputerBuilder WithMemory(
            Memory mem
            )
        {
            _computer.Mem = mem;
            return this;
        }        

        public ComputerBuilder WithExecOptions(Action<ExecOptions> configure)
        {
            var newExecOptions = new ExecOptions();
            configure(newExecOptions);
            _computer.ExecOptions = newExecOptions;
            return this;
        }        

        public ComputerBuilder WithInstructionToBeExecutedEventHandler(EventHandler<CPUInstructionToBeExecutedEventArgs> onInstructionToBeExecuted)
        {
            _computer.CPU.InstructionToBeExecuted += onInstructionToBeExecuted;
            return this;
        }
        public ComputerBuilder WithInstructionExecutedEventHandler(EventHandler<CPUInstructionExecutedEventArgs> onInstructionExecuted)
        {
            _computer.CPU.InstructionExecuted += onInstructionExecuted;
            return this;
        }

        public ComputerBuilder WithUnknownInstructionEventHandler(EventHandler<CPUUnknownOpCodeDetectedEventArgs> OnUnknownOpCodeDetected)
        {
            _computer.CPU.UnknownOpCodeDetected += OnUnknownOpCodeDetected;
            return this;
        }

        public Computer Build()
        {
            return _computer;
        }
    }
}