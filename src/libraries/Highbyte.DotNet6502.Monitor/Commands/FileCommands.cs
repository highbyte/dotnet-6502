using System.CommandLine;
using System.Globalization;

namespace Highbyte.DotNet6502.Monitor.Commands;

/// <summary>
/// </summary>
public static class FileCommands
{

    public static Command ConfigureFiles(this Command rootCommand, MonitorBase monitor, MonitorVariables monitorVariables)
    {
        rootCommand.AddCommand(BuildLoadCommand(monitor));
        rootCommand.AddCommand(BuildLoadManualCommand(monitor));
        rootCommand.AddCommand(BuildSaveCommand(monitor));
        return rootCommand;
    }

    private static Command BuildLoadCommand(MonitorBase monitor)
    {
        var addressArg = new Argument<string>()
        {
            Name = "address",
            Description = "Memory address (hex) to load the file into. If not specified, it's assumed the first two bytes of the file contains the load address.",
            Arity = ArgumentArity.ZeroOrOne
        }
        .MustBe16BitHex();

        var command = new Command("l", "Load 6502 binary file from file pick dialog into emulator memory.")
        {
            addressArg,
        };
        command.AddAlias("load");
        command.AddAlias("loadfilepicker");

        Func<string, Task<int>> handler = (string address) =>
        {
            ushort? forceLoadAtAddress;

            if (string.IsNullOrEmpty(address))
                forceLoadAtAddress = null;
            else
                forceLoadAtAddress = ushort.Parse(address, NumberStyles.AllowHexSpecifier, null);

            var loaded = monitor.LoadBinary(out var loadedAtAddress, out var fileLength, forceLoadAddress: forceLoadAtAddress);
            if (!loaded)
            {
                // If file could not be loaded at this time, probably because a Web/WASM file picker dialog is asynchronus
                return Task.FromResult((int)CommandResult.Ok);
            }

            monitor.WriteOutput($"File loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
            // Set PC to start of loaded file.
            monitor.Cpu.PC = loadedAtAddress;
            return Task.FromResult((int)CommandResult.Ok);

        };

        command.SetHandler(handler, addressArg);
        return command;
    }

    private static Command BuildLoadManualCommand(MonitorBase monitor)
    {
        var fileNameArg = new Argument<string>()
        {
            Name = "filename",
            Description = "Name of the binary file.",
            Arity = ArgumentArity.ExactlyOne
        };

        var addressArg = new Argument<string>()
        {
            Name = "address",
            Description = "Memory address (hex) to load the file into. If not specified, it's assumed the first two bytes of the file contains the load address.",
            Arity = ArgumentArity.ZeroOrOne
        }
        .MustBe16BitHex();

        var command = new Command("ll", "Load specified 6502 binary file into emulator memory.")
        {
            fileNameArg,
            addressArg
        };
        command.AddAlias("loadmanual");

        Func<string, string, Task<int>> handler = (string fileName, string address) =>
        {
            ushort? forceLoadAtAddress;

            if (string.IsNullOrEmpty(address))
                forceLoadAtAddress = null;
            else
                forceLoadAtAddress = ushort.Parse(address, NumberStyles.AllowHexSpecifier, null);

            bool loaded = monitor.LoadBinary(fileName, out var loadedAtAddress, out var fileLength, forceLoadAddress: forceLoadAtAddress);
            if (!loaded)
            {
                // If file could not be loaded, probably because it's not supported/implemented by the derived class.
                return Task.FromResult((int)CommandResult.Ok);
            }

            monitor.WriteOutput($"File loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
            // Set PC to start of loaded file.
            monitor.Cpu.PC = loadedAtAddress;
            return Task.FromResult((int)CommandResult.Ok);

        };

        command.SetHandler(handler, fileNameArg, addressArg);
        return command;
    }

    private static Command BuildSaveCommand(MonitorBase monitor)
    {
        var fileNameArg = new Argument<string>()
        {
            Name = "filename",
            Description = "Name of the binary file.",
            Arity = ArgumentArity.ExactlyOne
        };

        var startAddressArg = new Argument<string>()
        {
            Name = "startAddress",
            Description = "Start address (hex) of the memory area to save.",
            Arity = ArgumentArity.ExactlyOne
        }
        .MustBe16BitHex();

        var endAddressArg = new Argument<string>()
        {
            Name = "endAddress",
            Description = "End address (hex) of the memory area to save.",
            Arity = ArgumentArity.ExactlyOne
        }
        .MustBe16BitHex();

        var addFileHeaderArg = new Argument<string>()
        {
            Name = "addFileHeader",
            Description = "Optional. Set to n to NOT add a 2 byte file header with start address (usefull for data, not code)",
            Arity = ArgumentArity.ZeroOrOne,
        };
        addFileHeaderArg.AddValidator(
            a =>
            {
                var validationError =
                    a.Tokens
                    .Select(t => t.Value)
                    .Where(v => !string.IsNullOrEmpty(v) && !v.ToLower().Equals("y") && !v.ToLower().Equals("yes") && !v.ToLower().Equals("n") && !v.ToLower().Equals("no"))
                    .Select(_ => $"Argument '{addFileHeaderArg.Name}' must be either y, yes, n or no.")
                    .FirstOrDefault();
                if (validationError != null)
                    a.ErrorMessage = validationError;
            }
        );


        var command = new Command("s", "Save a binary from 6502 emulator memory to host file system.")
        {
            fileNameArg,
            startAddressArg,
            endAddressArg,
            addFileHeaderArg
        };
        command.AddAlias("save");

        Func<string, string, string, string, Task<int>> handler = (string fileName, string startAddress, string endAddress, string addFileHeader) =>
        {
            ushort startAddressValue = ushort.Parse(startAddress, NumberStyles.AllowHexSpecifier, null);
            ushort endAddressValue = ushort.Parse(endAddress, NumberStyles.AllowHexSpecifier, null);

            bool addFileHeaderWithLoadAddress = string.IsNullOrEmpty(addFileHeader)
                                                || (addFileHeader.ToLower() == "y" && addFileHeader.ToLower() == "yes");

            monitor.SaveBinary(fileName, startAddressValue, endAddressValue, addFileHeaderWithLoadAddress);

            return Task.FromResult((int)CommandResult.Ok);

        };

        command.SetHandler(handler, fileNameArg, startAddressArg, endAddressArg, addFileHeaderArg);
        return command;
    }
}
