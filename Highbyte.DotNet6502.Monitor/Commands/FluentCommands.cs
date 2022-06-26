using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Validation;

namespace Highbyte.DotNet6502.Monitor.Commands
{
    /// <summary>
    /// </summary>
    public class FluentCommands
    {
        public static CommandLineApplication Configure(Mon mon)
        {
            var app = new CommandLineApplication
            {
                Name = "",
                Description = "DotNet 6502 machine code monitor for the DotNet 6502 emulator library." + Environment.NewLine + 
                              "By Highbyte 2021" + Environment.NewLine +               
                              "Source at: https://github.com/highbyte/dotnet-6502",
                UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.StopParsingAndCollect
            };

            // Fix: Use custom HelpTextGentorator to avoid name/description of the application to be shown each time help text is shown.
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

                cmd.OnExecute(() =>
                {
                    ushort loadedAtAddres;
                    if(string.IsNullOrEmpty(address.Value))
                        mon.LoadBinary(fileName.Value, out loadedAtAddres);
                    else
                        mon.LoadBinary(fileName.Value, out loadedAtAddres, forceLoadAddress: ushort.Parse(address.Value));

                    Console.WriteLine($"File loaded at {loadedAtAddres.ToHex()}");
                    return 0;
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

                cmd.OnExecute(() =>
                {
                    ushort startAddress;
                    if(string.IsNullOrEmpty(start.Value))
                        startAddress = mon.Cpu.PC;
                    else
                        startAddress = ushort.Parse(start.Value, NumberStyles.AllowHexSpecifier, null);

                    ushort endAddress;
                    if(string.IsNullOrEmpty(end.Value))
                        endAddress = (ushort)(startAddress + 0x10);
                    else
                    {
                        endAddress = ushort.Parse(end.Value, NumberStyles.AllowHexSpecifier, null);
                        if(endAddress<startAddress)
                            endAddress = startAddress;
                    }
                    ushort currentAddress = startAddress;
                    while(currentAddress <= endAddress)
                    {
                        Console.WriteLine(OutputGen.GetInstructionDisassembly(mon.Cpu, mon.Mem, currentAddress));
                        var opCodeByte = mon.Mem[currentAddress];
                        int insSize;
                        if (!mon.Cpu.InstructionList.OpCodeDictionary.ContainsKey(opCodeByte))
                            insSize = 1;
                        else
                            insSize = mon.Cpu.InstructionList.GetOpCode(opCodeByte).Size;
                        currentAddress += (ushort)insSize;
                    }
                    return 0;
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

                cmd.OnExecute(() =>
                {
                    ushort startAddress;
                    if(string.IsNullOrEmpty(start.Value))
                        startAddress = 0x0000;
                    else
                        startAddress = ushort.Parse(start.Value, NumberStyles.AllowHexSpecifier, null);

                    ushort endAddress;
                    if(string.IsNullOrEmpty(end.Value))
                        endAddress = (ushort)(startAddress + (16*8) - 1);
                    else
                    {
                        endAddress = ushort.Parse(end.Value, NumberStyles.AllowHexSpecifier, null);
                        if(endAddress<startAddress)
                            endAddress = startAddress;
                    }

                    var list = OutputMemoryGen.GetFormattedMemoryList(mon.Mem, startAddress, endAddress);
                    foreach(var line in list)
                        Console.WriteLine(line);

                    return 0;
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

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        mon.Cpu.A = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        Console.WriteLine($"{OutputGen.GetRegisters(mon.Cpu)}");
                        return 0;                       
                    });
                });

                cmd.Command("x", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets X register.";
                    var regVal = setRegisterCmd.Argument("value", "Value of X register (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        mon.Cpu.X = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        Console.WriteLine($"{OutputGen.GetRegisters(mon.Cpu)}");
                        return 0;                       
                    });
                });

                cmd.Command("y", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets Y register.";
                    var regVal = setRegisterCmd.Argument("value", "Value of Y register (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        mon.Cpu.Y = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        Console.WriteLine($"{OutputGen.GetRegisters(mon.Cpu)}");
                        return 0;                       
                    });
                });

                cmd.Command("sp", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets SP (Stack Pointer).";
                    var regVal = setRegisterCmd.Argument("value", "Value of SP (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        mon.Cpu.SP = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        Console.WriteLine($"{OutputGen.GetPCandSP(mon.Cpu)}");
                        return 0;                       
                    });
                });

                cmd.Command("ps", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets processor status register.";
                    var regVal = setRegisterCmd.Argument("value", "Value of processor status register (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        mon.Cpu.ProcessorStatus.Value = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        Console.WriteLine($"PS={value}");
                        Console.WriteLine($"{OutputGen.GetStatus(mon.Cpu)}");
                        return 0;                       
                    });
                });

                cmd.Command("pc", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets PC (Program Counter).";
                    var regVal = setRegisterCmd.Argument("value", "Value of PC (hex).").IsRequired();
                    regVal.Validators.Add(new MustBe16BitHexValueValidator());

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        mon.Cpu.PC = ushort.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        Console.WriteLine($"{OutputGen.GetPCandSP(mon.Cpu)}");
                        return 0;                       
                    });
                });                                                    

                cmd.OnExecute(() =>
                {
                    Console.WriteLine(OutputGen.GetProcessorState(mon.Cpu, includeCycles: true));
                    return 0;
                });                
            }); 

            app.Command("g", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Change the PC (Program Counter) to the specified address and execute code.";
                cmd.AddName("goto");

                var address = cmd.Argument("address", "The address (hex) to start executing code at.").IsRequired();
                address.Validators.Add(new MustBe16BitHexValueValidator());
                var dontStopOnBRK = cmd.Option("--no-brk|-nb", "Prevent execution stop when BRK instruction encountered.", CommandOptionType.NoValue);
                cmd.OnExecute(() =>
                {
                    mon.Cpu.PC = ushort.Parse(address.Value, NumberStyles.AllowHexSpecifier, null);
                    ExecOptions execOptions;
                    if(dontStopOnBRK.HasValue())
                    {
                        execOptions = new ExecOptions();
                        Console.WriteLine($"Will never stop.");
                    }
                    else
                    {
                        execOptions = new ExecOptions
                        {
                            ExecuteUntilInstruction = OpCodeId.BRK,
                        };                        
                        Console.WriteLine($"Will stop on BRK instruction.");
                    }
                    Console.WriteLine($"Staring executing code at {mon.Cpu.PC.ToHex("",lowerCase:true)}");
                    mon.Computer.Run(execOptions);
                    Console.WriteLine($"Stopped at                {mon.Cpu.PC.ToHex("",lowerCase:true)}");
                    Console.WriteLine($"{OutputGen.GetLastInstructionDisassembly(mon.Cpu, mon.Mem)}");
                    return 0;
                });
            });

            app.Command("z", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Single step through instructions. Optionally execute a specified number of instructions.";

                var inscount = cmd.Argument<ulong>("inscount", "Number of instructions to execute. Defaults to 1.");
                inscount.DefaultValue = 1;
                cmd.OnExecute(() =>
                {
                    Console.WriteLine($"Executing code at {mon.Cpu.PC.ToHex("",lowerCase:true)} for {inscount.Value} instruction(s).");
                    var execOptions = new ExecOptions
                    {
                        MaxNumberOfInstructions = ulong.Parse(inscount.Value),
                    };                    
                    mon.Computer.Run(execOptions);
                    Console.WriteLine($"Last instruction:");
                    Console.WriteLine($"{OutputGen.GetLastInstructionDisassembly(mon.Cpu, mon.Mem)}");
                    return 0;
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

                cmd.OnExecute(() =>
                {
                    var address = ushort.Parse(memAddress.Value, NumberStyles.AllowHexSpecifier, null);
                    List<byte> bytes = new();
                    foreach(var val in memValues.Values)
                        bytes.Add(byte.Parse(val, NumberStyles.AllowHexSpecifier, null));
                    foreach(var val in bytes)
                        mon.Mem[address++] = val;
                    return 0;
                });
            });             

            app.Command("q", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Quit monitor.";
                cmd.AddName("quit");
                cmd.AddName("x");
                cmd.AddName("exit");
                cmd.OnExecute(() =>
                {
                    Console.WriteLine($"Quiting.");
                    return 2;
                });
            });

            app.OnExecute(() =>
            {
                Console.WriteLine("Unknown command.");
                Console.WriteLine("Help: ?|help|-?|--help");
                //app.ShowHelp();
                return 1;
            });

            return app;
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

}