using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor.Commands
{
    /// <summary>
    /// </summary>
    public static class FileCommands
    {
        public static CommandLineApplication ConfigureFiles(this CommandLineApplication app, MonitorBase monitor)
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
                    if (string.IsNullOrEmpty(address.Value))
                        monitor.LoadBinary(fileName.Value, out loadedAtAddress);
                    else
                        monitor.LoadBinary(fileName.Value, out loadedAtAddress, forceLoadAddress: ushort.Parse(address.Value));

                    monitor.WriteOutput($"File loaded at {loadedAtAddress.ToHex()}");
                    return (int)CommandResult.Ok;
                });
            });

            return app;
        }
    }
}