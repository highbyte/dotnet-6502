using System.ComponentModel.DataAnnotations;
using System.Globalization;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Validation;

namespace Highbyte.DotNet6502.Monitor
{
    /// <summary>
    /// </summary>
    public class FluentCommands
    {
        public static CommandLineApplication Configure(MonitorBase monitor)
        {
            var app = new CommandLineApplication(NullConsole.Singleton, monitor.Options.DefaultDirectory)
            {
                Name = "",
                Description = "DotNet 6502 machine code monitor for the DotNet 6502 emulator library." + Environment.NewLine +
                              "By Highbyte 2022" + Environment.NewLine +
                              "Source at: https://github.com/highbyte/dotnet-6502",
                UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.StopParsingAndCollect
            };

            // Fix: Use custom Help Text Generator to avoid name/description of the application to be shown each time help text is shown.
            app.HelpTextGenerator = new CustomHelpTextGenerator();
            // Fix: To avoid CommandLineUtils to the name of the application at the end of the help text: Don't use HelpOption on app-level, instead set it on each command below.
            //app.HelpOption(inherited: true);

            app.Command("l", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Load a 6502 binary into emulator memory.";
                cmd.AddName("load");

                var fileName = cmd.Argument("filename", "Name of the binary file.")
                    .IsRequired()
                    .Accepts(v => v.ExistingFile());

                var address = cmd.Argument("address", "Memory address (hex) to load the file into. If not specified, it's assumed the first two bytes of the file contains the load address.");
                address.Validators.Add(new MustBe16BitHexValueValidator());

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return WriteValidationError(monitor, validationResult);
                });

                cmd.OnExecute(() =>
                {
                    ushort loadedAtAddress;
                    if (string.IsNullOrEmpty(address.Value))
                        monitor.LoadBinary(fileName.Value, out loadedAtAddress);
                    else
                        monitor.LoadBinary(fileName.Value, out loadedAtAddress, forceLoadAddress: ushort.Parse(address.Value));

                    monitor.WriteOutput($"File loaded at {loadedAtAddress.ToHex()}");
                    return (int)CommandResult.Ok;
                });
            });

            app.Command("d", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Disassembles 6502 code from emulator memory.";

                var start = cmd.Argument("start", "Start address (hex). If not specified, the current PC address is used.");
                start.Validators.Add(new MustBe16BitHexValueValidator());

                var end = cmd.Argument("end", "End address (hex). If not specified, a default number of addresses will be shown from start.");
                end.Validators.Add(new MustBe16BitHexValueValidator());
                end.Validators.Add(new GreaterThan16bitValidator(start));

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return WriteValidationError(monitor, validationResult);
                });

                cmd.OnExecute(() =>
                {
                    ushort startAddress;
                    if (string.IsNullOrEmpty(start.Value))
                        startAddress = monitor.Cpu.PC;
                    else
                        startAddress = ushort.Parse(start.Value, NumberStyles.AllowHexSpecifier, null);

                    ushort endAddress;
                    if (string.IsNullOrEmpty(end.Value))
                    {
                        const int DEFAULT_BYTES_TO_SHOW = 0x10;
                        var endAddressDelta = DEFAULT_BYTES_TO_SHOW - 1;
                        if ((uint)((uint)startAddress + (uint)endAddressDelta) <= 0xffff)
                            endAddress = (ushort)(startAddress + endAddressDelta);
                        else
                            endAddress = 0xffff;
                        endAddress = (ushort)(startAddress + endAddressDelta);
                    }
                    else
                    {
                        endAddress = ushort.Parse(end.Value, NumberStyles.AllowHexSpecifier, null);
                        if (endAddress < startAddress)
                            endAddress = startAddress;
                    }
                    ushort currentAddress = startAddress;
                    bool cont = true;
                    while (cont)
                    {
                        monitor.WriteOutput(OutputGen.GetInstructionDisassembly(monitor.Cpu, monitor.Mem, currentAddress));
                        var opCodeByte = monitor.Mem[currentAddress];
                        int insSize;
                        if (!monitor.Cpu.InstructionList.OpCodeDictionary.ContainsKey(opCodeByte))
                            insSize = 1;
                        else
                            insSize = monitor.Cpu.InstructionList.GetOpCode(opCodeByte).Size;

                        if (currentAddress < endAddress && ((uint)(currentAddress + insSize) <= 0xffff))
                            currentAddress += (ushort)insSize;
                        else
                            cont = false;
                    }
                    return (int)CommandResult.Ok;
                });
            });

            app.Command("m", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Show contents of emulator memory in bytes.";
                cmd.AddName("mem");

                var start = cmd.Argument("start", "Start address (hex). If not specified, the 0000 address is used.");
                start.Validators.Add(new MustBe16BitHexValueValidator());

                var end = cmd.Argument("end", "End address (hex). If not specified, a default number of memory locations will be shown from start.");
                end.Validators.Add(new MustBe16BitHexValueValidator());
                end.Validators.Add(new GreaterThan16bitValidator(start));

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return WriteValidationError(monitor, validationResult);
                });

                cmd.OnExecute(() =>
                {
                    ushort startAddress;
                    if (string.IsNullOrEmpty(start.Value))
                        startAddress = 0x0000;
                    else
                        startAddress = ushort.Parse(start.Value, NumberStyles.AllowHexSpecifier, null);

                    ushort endAddress;
                    if (string.IsNullOrEmpty(end.Value))
                    {
                        const int DEFAULT_BYTES_TO_SHOW = (16 * 8);
                        ushort endAddressDelta = DEFAULT_BYTES_TO_SHOW - 1;
                        if ((uint)((uint)startAddress + (uint)endAddressDelta) <= 0xffff)
                            endAddress = (ushort)(startAddress + endAddressDelta);
                        else
                            endAddress = 0xffff;
                    }
                    else
                    {
                        endAddress = ushort.Parse(end.Value, NumberStyles.AllowHexSpecifier, null);
                        if (endAddress < startAddress)
                            endAddress = startAddress;
                    }

                    var list = OutputMemoryGen.GetFormattedMemoryList(monitor.Mem, startAddress, endAddress);
                    foreach (var line in list)
                        monitor.WriteOutput(line);

                    return (int)CommandResult.Ok;
                });
            });

            app.Command("r", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Show processor status and registers. CY = #cycles executed.";
                cmd.AddName("reg");

                cmd.Command("a", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets A register.";
                    var regVal = setRegisterCmd.Argument("value", "Value of A register (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnValidationError((ValidationResult validationResult) =>
                    {
                        return WriteValidationError(monitor, validationResult);
                    });

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        monitor.Cpu.A = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        monitor.WriteOutput($"{OutputGen.GetRegisters(monitor.Cpu)}");
                        return (int)CommandResult.Ok;
                    });
                });

                cmd.Command("x", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets X register.";
                    var regVal = setRegisterCmd.Argument("value", "Value of X register (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnValidationError((ValidationResult validationResult) =>
                    {
                        return WriteValidationError(monitor, validationResult);
                    });

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        monitor.Cpu.X = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        monitor.WriteOutput($"{OutputGen.GetRegisters(monitor.Cpu)}");
                        return (int)CommandResult.Ok;
                    });
                });

                cmd.Command("y", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets Y register.";
                    var regVal = setRegisterCmd.Argument("value", "Value of Y register (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnValidationError((ValidationResult validationResult) =>
                    {
                        return WriteValidationError(monitor, validationResult);
                    });

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        monitor.Cpu.Y = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        monitor.WriteOutput($"{OutputGen.GetRegisters(monitor.Cpu)}");
                        return (int)CommandResult.Ok;
                    });
                });

                cmd.Command("sp", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets SP (Stack Pointer).";
                    var regVal = setRegisterCmd.Argument("value", "Value of SP (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnValidationError((ValidationResult validationResult) =>
                    {
                        return WriteValidationError(monitor, validationResult);
                    });

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        monitor.Cpu.SP = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        monitor.WriteOutput($"{OutputGen.GetPCandSP(monitor.Cpu)}");
                        return (int)CommandResult.Ok;
                    });
                });

                cmd.Command("ps", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets processor status register.";
                    var regVal = setRegisterCmd.Argument("value", "Value of processor status register (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnValidationError((ValidationResult validationResult) =>
                    {
                        return WriteValidationError(monitor, validationResult);
                    });

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        monitor.Cpu.ProcessorStatus.Value = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        monitor.WriteOutput($"PS={value}");
                        monitor.WriteOutput($"{OutputGen.GetStatus(monitor.Cpu)}");
                        return (int)CommandResult.Ok;
                    });
                });

                cmd.Command("pc", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets PC (Program Counter).";
                    var regVal = setRegisterCmd.Argument("value", "Value of PC (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe16BitHexValueValidator());

                    setRegisterCmd.OnValidationError((ValidationResult validationResult) =>
                    {
                        return WriteValidationError(monitor, validationResult);
                    });

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        monitor.Cpu.PC = ushort.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        monitor.WriteOutput($"{OutputGen.GetPCandSP(monitor.Cpu)}");
                        return (int)CommandResult.Ok;
                    });
                });

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return WriteValidationError(monitor, validationResult);
                });

                cmd.OnExecute(() =>
                {
                    monitor.WriteOutput(OutputGen.GetProcessorState(monitor.Cpu, includeCycles: true));
                    return (int)CommandResult.Ok;
                });
            });

            app.Command("g", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Change the PC (Program Counter) to the specified address continue execution.";
                cmd.AddName("goto");

                var address = cmd.Argument("address", "Optional address (hex) to start executing code at.");
                address.Validators.Add(new MustBe16BitHexValueValidator());
                var dontStopOnBRK = cmd.Option("--no-brk|-nb", "Prevent execution stop when BRK instruction encountered.", CommandOptionType.NoValue);

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return WriteValidationError(monitor, validationResult);
                });

                cmd.OnExecute(() =>
                {
                    if (string.IsNullOrEmpty(address.Value))
                        return (int)CommandResult.Continue;

                    monitor.Cpu.PC = ushort.Parse(address.Value, NumberStyles.AllowHexSpecifier, null);
                    ExecOptions execOptions;
                    if (dontStopOnBRK.HasValue())
                    {
                        execOptions = new ExecOptions();
                        monitor.WriteOutput($"Will never stop.");
                    }
                    else
                    {
                        execOptions = new ExecOptions
                        {
                            ExecuteUntilInstruction = OpCodeId.BRK,
                        };                        
                        monitor.WriteOutput($"Will stop on BRK instruction.");
                    }
                    monitor.WriteOutput($"Staring executing code at {monitor.Cpu.PC.ToHex("",lowerCase:true)}");
                    // monitor.SystemRunner.Run();
                    // monitor.WriteOutput($"Stopped at                {monitor.Cpu.PC.ToHex("",lowerCase:true)}");
                    // monitor.WriteOutput($"{OutputGen.GetLastInstructionDisassembly(monitor.Cpu, monitor.Mem)}");
                    return (int)CommandResult.Continue;
                });
            });

            app.Command("z", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Single step through instructions. Optionally execute a specified number of instructions.";

                var inscount = cmd.Argument<ulong>("inscount", "Number of instructions to execute. Defaults to 1.");
                inscount.DefaultValue = 1;

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return WriteValidationError(monitor, validationResult);
                });

                cmd.OnExecute(() =>
                {
                    //monitor.WriteOutput($"Executing code at {monitor.Cpu.PC.ToHex("",lowerCase:true)} for {inscount.Value} instruction(s).");
                    var execOptions = new ExecOptions
                    {
                        MaxNumberOfInstructions = ulong.Parse(inscount.Value),
                    };
                    monitor.Cpu.Execute(monitor.Mem, execOptions);
                    //monitor.WriteOutput($"Last instruction:");
                    monitor.WriteOutput($"{OutputGen.GetLastInstructionDisassembly(monitor.Cpu, monitor.Mem)}");
                    return (int)CommandResult.Ok;
                });
            });

            app.Command("f", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Fill memory at specified address with a list of bytes. Example: f 1000 20 ff ab 30";
                cmd.AddName("fill");

                var memAddress = cmd.Argument("address", "Memory address (hex).").IsRequired();
                memAddress.Validators.Add(new MustBe16BitHexValueValidator());

                var memValues = cmd.Argument("values", "List of byte values (hex). Example: 20 ff ab 30").IsRequired();
                memValues.MultipleValues = true;
                memValues.Validators.Add(new MustBe8BitHexValueValidator());

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return WriteValidationError(monitor, validationResult);
                });

                cmd.OnExecute(() =>
                {
                    var address = ushort.Parse(memAddress.Value, NumberStyles.AllowHexSpecifier, null);
                    List<byte> bytes = new();
                    foreach (var val in memValues.Values)
                        bytes.Add(byte.Parse(val, NumberStyles.AllowHexSpecifier, null));
                    foreach (var val in bytes)
                        monitor.Mem[address++] = val;
                    return (int)CommandResult.Ok;
                });
            });

            app.Command("q", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Quit monitor.";
                cmd.AddName("quit");
                cmd.AddName("x");
                cmd.AddName("exit");

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return WriteValidationError(monitor, validationResult);
                });

                cmd.OnExecute(() =>
                {
                    //monitor.WriteOutput($"Quiting.");
                    return (int)CommandResult.Quit;
                });
            });

            app.OnExecute(() =>
            {
                monitor.WriteOutput("Unknown command.", MessageSeverity.Error);
                monitor.WriteOutput("Help: ?|help|-?|--help", MessageSeverity.Information);
                return (int)CommandResult.Error;
            });

            return app;
        }

        private static int WriteValidationError(MonitorBase monitor, ValidationResult validationResult)
        {
            monitor.WriteOutput(!string.IsNullOrEmpty(validationResult.ErrorMessage)
                ? validationResult.ErrorMessage
                : "Unknown validation message", MessageSeverity.Error);
            return (int)CommandResult.Ok;
        }
    }

    class MustBe16BitHexValueValidator : IArgumentValidator
    {
        public ValidationResult GetValidationResult(CommandArgument argument, ValidationContext context)
        {
            // This validator only runs if there is a value
            if (string.IsNullOrEmpty(argument.Value))
                return ValidationResult.Success;  //return new ValidationResult($"{argument.Name} cannot be empty");

            var addressString = argument.Value;
            bool validAddress = ushort.TryParse(addressString, NumberStyles.AllowHexSpecifier, null, out ushort word);
            if (!validAddress)
            {
                return new ValidationResult($"The value for {argument.Name} must be a 16-bit hex address");
            }
            return ValidationResult.Success;
        }
    }

    class MustBe8BitHexValueValidator : IArgumentValidator
    {
        public ValidationResult GetValidationResult(CommandArgument argument, ValidationContext context)
        {
            // This validator only runs if there is a value
            if (string.IsNullOrEmpty(argument.Value))
                return ValidationResult.Success;  //return new ValidationResult($"{argument.Name} cannot be empty");

            bool validByte = byte.TryParse(argument.Value, NumberStyles.AllowHexSpecifier, null, out byte byteValue);
            if (!validByte)
            {
                return new ValidationResult($"The value for {argument.Name} must be a 8-bit hex number");
            }
            return ValidationResult.Success;
        }
    }

    class GreaterThan16bitValidator : IArgumentValidator
    {
        private readonly CommandArgument _otherArgument;
        private readonly bool _ignoreUndefined;

        public GreaterThan16bitValidator(CommandArgument otherArgument, bool ignoreUndefined = true)
        {
            _otherArgument = otherArgument;
            _ignoreUndefined = ignoreUndefined;
        }

        public ValidationResult GetValidationResult(CommandArgument argument, ValidationContext context)
        {
            if (_ignoreUndefined && string.IsNullOrEmpty(argument.Value))
                return ValidationResult.Success;

            var value = ushort.Parse(argument.Value, NumberStyles.AllowHexSpecifier, null);
            var otherValue = ushort.Parse(_otherArgument.Value, NumberStyles.AllowHexSpecifier, null);
            return value > otherValue ? ValidationResult.Success : new ValidationResult($"The 16 bit value {argument.Name} ({argument.Value}) must higher than {_otherArgument.Name} ({_otherArgument.Value})");
        }
    }
}