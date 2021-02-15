using System;
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

                var address = loadCmd.Argument("address", "Memory address to load the file into. If not specified, it's assumed the first two bytes of the file contains the load address");
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
                disassembleCmd.Description = "Disassembles 6502 code in memory";

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

            app.Command("m", disassembleCmd =>
            {
                disassembleCmd.Description = "Show contents of memory in bytes";

                var start = disassembleCmd.Argument("start", "Start address (hex). If not specified, the 0000 address is used.");
                start.Validators.Add(new MustBe16BitHexValueValidator());

                var end = disassembleCmd.Argument("end", "End address (hex). If not specified, a default number of memory locations will be shown from start");
                end.Validators.Add(new MustBe16BitHexValueValidator());

                disassembleCmd.OnExecute(() =>
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

}