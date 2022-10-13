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
                cmd.Description = "Load a 6502 binary into emulator memory.";
                cmd.AddName("load");

                var fileName = cmd.Argument("filename", "Name of the binary file.")
                    .IsRequired()
                    .Accepts(v => v.ExistingFile());

                var address = cmd.Argument("address", "Memory address (hex) to load the file into. If not specified, it's assumed the first two bytes of the file contains the load address.");
                address.Validators.Add(new MustBe16BitHexValueValidator());

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return monitor.WriteValidationError(validationResult);
                });

                cmd.OnExecute(() =>
                {
                    ushort loadedAtAddress;
                    ushort fileLength;
                    if (string.IsNullOrEmpty(address.Value))
                    {
                        monitor.LoadBinary(fileName.Value, out loadedAtAddress, out fileLength);
                    }
                    else
                    {
                        ushort forceLoadAtAddress = ushort.Parse(address.Value, NumberStyles.AllowHexSpecifier, null);
                        monitor.LoadBinary(fileName.Value, out loadedAtAddress, out fileLength, forceLoadAddress: forceLoadAtAddress);
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

                var addFileHeader = cmd.Argument("addFileHeader", "Optional. Set to y add a 2 byte file header with start address (useful for programs, not data)")
                    .Accepts(arg => arg.Values("y", "yes", "n", "no"));

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return monitor.WriteValidationError(validationResult);
                });

                cmd.OnExecute(() =>
                {
                    ushort startAddressValue = ushort.Parse(startAddress.Value, NumberStyles.AllowHexSpecifier, null);
                    ushort endAddressValue = ushort.Parse(endAddress.Value, NumberStyles.AllowHexSpecifier, null);

                    bool addFileHeaderWithLoadAddress = !string.IsNullOrEmpty(addFileHeader.Value)
                                                        && (addFileHeader.Value.ToLower() == "y" || addFileHeader.Value.ToLower() == "yes");

                    monitor.SaveBinary(fileName.Value, startAddressValue, endAddressValue, addFileHeaderWithLoadAddress);

                    monitor.WriteOutput($"File saved to {fileName.Value}");
                    return (int)CommandResult.Ok;
                });
            });

            return app;
        }
    }
}