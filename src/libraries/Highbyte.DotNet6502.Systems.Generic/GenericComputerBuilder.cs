using System.Diagnostics;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Generic;

public class GenericComputerBuilder
{
    private readonly GenericComputer _genericComputer;
    private readonly ILoggerFactory _loggerFactory;

    public GenericComputerBuilder(ILoggerFactory loggerFactory) : this(loggerFactory, new GenericComputerConfig()) { }

    public GenericComputerBuilder(ILoggerFactory loggerFactory, GenericComputerConfig genericComputerConfig)
    {
        _loggerFactory = loggerFactory;
        _genericComputer = new GenericComputer(genericComputerConfig, loggerFactory);
    }

    public GenericComputerBuilder WithCPU()
    {
        _genericComputer.CPU = new CPU(_loggerFactory);
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

    public static GenericComputer SetupGenericComputerFromConfig(GenericComputerConfig emulatorConfig, ILoggerFactory loggerFactory)
    {
        emulatorConfig.Validate();

        var mem = new Memory();

        ushort loadedAtAddress;
        ushort fileLength;
        if (!string.IsNullOrEmpty(emulatorConfig.ProgramBinaryFile))
        {
            // .prg is loaded from file.
            Debug.WriteLine($"Loading 6502 prg file from binary file.");

            Debug.WriteLine($"{emulatorConfig.ProgramBinaryFile}");
            BinaryLoader.Load(
                mem,
                emulatorConfig.ProgramBinaryFile,
                out loadedAtAddress,
                out fileLength);
        }
        else
        {
            Debug.WriteLine($"Loading 6502 prg file from byte array.");
            // .prg file was passed as binary array.
            var prgBytes = emulatorConfig.ProgramBinary;
            // First two bytes of binary file is assumed to be start address, little endian notation.
            loadedAtAddress = ByteHelpers.ToLittleEndianWord(prgBytes[0], prgBytes[1]);
            // The rest of the bytes are considered the code & data
            var codeAndDataActual = new byte[prgBytes.Length - 2];
            Array.Copy(prgBytes, 2, codeAndDataActual, 0, prgBytes.Length - 2);
            mem.StoreData(loadedAtAddress, codeAndDataActual);
            fileLength = (ushort)codeAndDataActual.Length;
        }

        // Initialize emulator with CPU, memory, and execution parameters
        var computerBuilder = new GenericComputerBuilder(loggerFactory, emulatorConfig);
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
