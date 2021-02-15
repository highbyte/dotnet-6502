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
                Description = "Monitor commands",
                UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.StopParsingAndCollect
            };

            app.HelpOption(inherited: true);

            app.Command("l", loadCmd =>
            {
                loadCmd.Description = "Load a 6502 binary into emulator memory";

                var fileName = loadCmd.Argument("filename", "Name of the binary file")
                    .IsRequired()
                    .Accepts(v => v.ExistingFile());

                var address = loadCmd.Argument("address", "Memory address (hex) to load the file into. If not specified, it's assumed the first two bytes of the file contains the load address");
                address.Validators.Add(new MustBe16BitHexValueValidator());

                loadCmd.OnExecute(() =>
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

            app.Command("d", disassembleCmd =>
            {
                disassembleCmd.Description = "Disassembles 6502 code from emulator memory";

                var start = disassembleCmd.Argument("start", "Start address (hex). If not specified, the current PC address is used.");
                start.Validators.Add(new MustBe16BitHexValueValidator());

                var end = disassembleCmd.Argument("end", "End address (hex). If not specified, a default number of addresses will be shown from start");
                end.Validators.Add(new MustBe16BitHexValueValidator());

                disassembleCmd.OnExecute(() =>
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

            app.Command("m", memoryCmd =>
            {
                memoryCmd.Description = "Show contents of emulator memory in bytes";

                var start = memoryCmd.Argument("start", "Start address (hex). If not specified, the 0000 address is used.");
                start.Validators.Add(new MustBe16BitHexValueValidator());

                var end = memoryCmd.Argument("end", "End address (hex). If not specified, a default number of memory locations will be shown from start");
                end.Validators.Add(new MustBe16BitHexValueValidator());

                memoryCmd.OnExecute(() =>
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

            app.Command("r", registersCmd =>
            {
                registersCmd.Description = "Show processor status and registers. CY = #cycles executed.";

                registersCmd.Command("a", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets A register";
                    var regVal = setRegisterCmd.Argument("value", "Value of A register (hex)").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        mon.Cpu.A = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        Console.WriteLine($"{OutputGen.GetRegisters(mon.Cpu)}");
                        return 0;                       
                    });
                });

                registersCmd.Command("x", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets X register";
                    var regVal = setRegisterCmd.Argument("value", "Value of X register (hex)").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        mon.Cpu.X = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        Console.WriteLine($"{OutputGen.GetRegisters(mon.Cpu)}");
                        return 0;                       
                    });
                });

                registersCmd.Command("y", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets Y register";
                    var regVal = setRegisterCmd.Argument("value", "Value of Y register (hex)").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        mon.Cpu.Y = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        Console.WriteLine($"{OutputGen.GetRegisters(mon.Cpu)}");
                        return 0;                       
                    });
                });

                registersCmd.Command("sp", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets SP (Stack Pointer)";
                    var regVal = setRegisterCmd.Argument("value", "Value of SP (hex)").IsRequired();
                    regVal.Validators.Add(new MustBe8BitHexValueValidator());

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        mon.Cpu.SP = byte.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        Console.WriteLine($"{OutputGen.GetPCandSP(mon.Cpu)}");
                        return 0;                       
                    });
                });

                registersCmd.Command("ps", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets processor status register";
                    var regVal = setRegisterCmd.Argument("value", "Value of processor status register (hex)").IsRequired();
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

                registersCmd.Command("pc", setRegisterCmd =>
                    {
                    setRegisterCmd.Description = "Sets PC (Program Counter)";
                    var regVal = setRegisterCmd.Argument("value", "Value of PC (hex)").IsRequired();
                    regVal.Validators.Add(new MustBe16BitHexValueValidator());

                    setRegisterCmd.OnExecute(() =>
                    {
                        var value = regVal.Value;
                        mon.Cpu.PC = ushort.Parse(value, NumberStyles.AllowHexSpecifier, null);
                        Console.WriteLine($"{OutputGen.GetPCandSP(mon.Cpu)}");
                        return 0;                       
                    });
                });                                                    

                registersCmd.OnExecute(() =>
                {
                    Console.WriteLine(OutputGen.GetProcessorState(mon.Cpu, includeCycles: true));
                    return 0;
                });                
            }); 


            app.Command("g", gotoCmd =>
            {
                gotoCmd.Description = "Change the PC (Program Counter) to the specified address and execute code.";
                var address = gotoCmd.Argument("address", "The address (hex) to start executing code at").IsRequired();
                address.Validators.Add(new MustBe16BitHexValueValidator());
                var dontStopOnBRK = gotoCmd.Option("--no-brk|-nb", "Prevent execution stop when BRK instruction encountered.", CommandOptionType.NoValue);
                gotoCmd.OnExecute(() =>
                {
                    mon.Cpu.PC = ushort.Parse(address.Value, NumberStyles.AllowHexSpecifier, null);
                    if(dontStopOnBRK.HasValue())
                    {
                        mon.Computer.ExecOptions.ExecuteUntilInstruction = null;
                        Console.WriteLine($"Will never stop.");
                    }
                    else
                    {
                        mon.Computer.ExecOptions.ExecuteUntilInstruction = OpCodeId.BRK;
                        Console.WriteLine($"Will stop on BRK instruction.");
                    }
                    Console.WriteLine($"Staring executing code at {mon.Cpu.PC.ToHex("",lowerCase:true)}");
                    mon.Computer.Run();
                    Console.WriteLine($"Stopped at                {mon.Cpu.PC.ToHex("",lowerCase:true)}");
                    Console.WriteLine($"{OutputGen.GetLastInstructionDisassembly(mon.Cpu, mon.Mem)}");
                    return 0;
                });
            });

            app.Command("f", fillMemoryCmd =>
            {
                fillMemoryCmd.Description = "Fill memory att specified address with a list of bytes. Example: f 1000 20 ff ab 30";
                
                var memAddress = fillMemoryCmd.Argument("address", "Memory address (hex)").IsRequired();
                memAddress.Validators.Add(new MustBe16BitHexValueValidator());

                var memValues = fillMemoryCmd.Argument("values", "List of byte values (hex). Example: 20 ff ab 30").IsRequired();
                memValues.MultipleValues = true;
                memValues.Validators.Add(new MustBe8BitHexValueValidator());

                fillMemoryCmd.OnExecute(() =>
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

            app.Command("q", quitCmd =>
            {
                quitCmd.Description = "Quit monitor";
                quitCmd.OnExecute(() =>
                {
                    Console.WriteLine($"Quiting.");
                    return 2;
                });
            });

            app.OnExecute(() =>
            {
                Console.WriteLine("Specify a command");
                app.ShowHelp();
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