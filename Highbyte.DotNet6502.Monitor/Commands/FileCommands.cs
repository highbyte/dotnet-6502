using System.ComponentModel.DataAnnotations;
using System.Globalization;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor.Commands
{
    /// <summary>
    /// </summary>
    public static class FileCommands
    {
        public static CommandLineApplication ConfigureFiles(this CommandLineApplication app, MonitorBase monitor, MonitorVariables monitorVariables)
        {
            app.Command("l", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Load 6502 binary file from file pick dialog into emulator memory.";
                cmd.AddName("load from file picker");

                var address = cmd.Argument("address", "Memory address (hex) to load the file into. If not specified, it's assumed the first two bytes of the file contains the load address.");
                address.Validators.Add(new MustBe16BitHexValueValidator());

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return monitor.WriteValidationError(validationResult);
                });

                cmd.OnExecute(() =>
                {
                    ushort? forceLoadAtAddress;

                    if (string.IsNullOrEmpty(address.Value))
                        forceLoadAtAddress = null;
                    else
                        forceLoadAtAddress = ushort.Parse(address.Value, NumberStyles.AllowHexSpecifier, null);

                    var loaded = monitor.LoadBinary(out var loadedAtAddress, out var fileLength, forceLoadAddress: forceLoadAtAddress);
                    if (!loaded)
                    {
                        // If file could not be loaded at this time, probably because a Web/WASM file picker dialog is asynchronus
                        return (int)CommandResult.Ok;
                    }

                    monitor.WriteOutput($"File loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
                    return (int)CommandResult.Ok;

                });
            });
            app.Command("ll", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Load specifiled 6502 binary file into emulator memory.";
                cmd.AddName("load file");

                var fileName = cmd.Argument("filename", "Name of the binary file.")
                    .IsRequired();
                    //.Accepts(v => v.ExistingFile());  // Check file is done in LoadBinary(...) implementation

                var address = cmd.Argument("address", "Memory address (hex) to load the file into. If not specified, it's assumed the first two bytes of the file contains the load address.");
                address.Validators.Add(new MustBe16BitHexValueValidator());

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return monitor.WriteValidationError(validationResult);
                });

                cmd.OnExecute(() =>
                {
                    ushort? forceLoadAtAddress;

                    if (string.IsNullOrEmpty(address.Value))
                        forceLoadAtAddress = null;
                    else
                        forceLoadAtAddress = ushort.Parse(address.Value, NumberStyles.AllowHexSpecifier, null);

                    bool loaded = monitor.LoadBinary(fileName.Value, out var loadedAtAddress, out var fileLength, forceLoadAddress: forceLoadAtAddress);
                    if (!loaded)
                    {
                        // If file could not be loaded, probably because it's not supported/implemented by the derived class.
                        return (int)CommandResult.Ok;
                    }

                    monitor.WriteOutput($"File loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
                    return (int)CommandResult.Ok;

                });
            });


            app.Command("s", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "Save a binary from 6502 emulator memory to host file system.";
                cmd.AddName("save");

                var fileName = cmd.Argument("filename", "Name of the binary file.")
                    .IsRequired();

                var startAddress = cmd.Argument("startAddress", "Start address (hex) of the memory area to save.")
                    .IsRequired();
                startAddress.Validators.Add(new MustBe16BitHexValueValidator());

                var endAddress = cmd.Argument("endAddress", "End address (hex) of the memory area to save.")
                    .IsRequired();
                endAddress.Validators.Add(new MustBe16BitHexValueValidator());

                var addFileHeader = cmd.Argument("addFileHeader", "Optional. Set to n to NOT add a 2 byte file header with start address (usefull for data, not code)")
                    .Accepts(arg => arg.Values("y", "yes", "n", "no"));

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return monitor.WriteValidationError(validationResult);
                });

                cmd.OnExecute(() =>
                {
                    ushort startAddressValue = ushort.Parse(startAddress.Value, NumberStyles.AllowHexSpecifier, null);
                    ushort endAddressValue = ushort.Parse(endAddress.Value, NumberStyles.AllowHexSpecifier, null);

                    bool addFileHeaderWithLoadAddress = string.IsNullOrEmpty(addFileHeader.Value)
                                                        || (addFileHeader.Value.ToLower() == "y" && addFileHeader.Value.ToLower() == "yes");

                    monitor.SaveBinary(fileName.Value, startAddressValue, endAddressValue, addFileHeaderWithLoadAddress);

                    monitor.WriteOutput($"File saved to {fileName.Value}");
                    return (int)CommandResult.Ok;
                });
            });

            return app;
        }
    }
}
