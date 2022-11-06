using System.ComponentModel.DataAnnotations;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Monitor.Commands;
using Highbyte.DotNet6502.Monitor.SystemSpecific;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Systems.Commodore64.Monitor
{
    /// <summary>
    /// C64-specific monitor commands.
    /// </summary>
    public class C64MonitorCommands : ISystemMonitorCommands
    {
        public void Configure(CommandLineApplication app, MonitorBase monitor)
        {
            app.Command("lb", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "C64 - Load a Commodore Basic 2.0 PRG file from file picker dialog.";
                cmd.AddName("loadbasic from filepicker");

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return monitor.WriteValidationError(validationResult);
                });

                cmd.OnExecute(() =>
                {
                    // Basic file should have a start address of 0801 stored as the two first bytes (little endian order, 01 08).
                    var loaded = monitor.LoadBinary(out var loadedAtAddress, out var fileLength, null, AfterLoadBasic);
                    if (!loaded)
                    {
                        // If file could not be loaded at this time, probably because a Web/WASM file picker dialog is asynchronus
                        return (int)CommandResult.Ok;
                    }
                    AfterLoadBasic(monitor, loadedAtAddress, fileLength);
                    return (int)CommandResult.Ok;
                });
            });
            app.Command("llb", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "C64 - Load a Commodore Basic 2.0 PRG file from host file system.";
                cmd.AddName("loadbasic file");

                var fileName = cmd.Argument("filename", "Name of the Basic file.")
                    .IsRequired();
                    //.Accepts(v => v.ExistingFile()); // File exists check is done in LoadBinary(...) implementation.

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return monitor.WriteValidationError(validationResult);
                });

                cmd.OnExecute(() =>
                {
                    // Basic file should have a start address of 0801 stored as the two first bytes (little endian order, 01 08).
                    bool loaded = monitor.LoadBinary(fileName.Value, out var loadedAtAddress, out var fileLength);
                    if (!loaded)
                    {
                        // If file could not be loaded, probably because it's not supported/implemented by the derived class.
                        return (int)CommandResult.Ok;
                    }
                    AfterLoadBasic(monitor, loadedAtAddress, fileLength);
                    return (int)CommandResult.Ok;

                });
            });

            app.Command("sb", cmd =>
            {
                cmd.HelpOption(inherited: true);
                cmd.Description = "C64 - Save a Commodore Basic 2.0 PRG file to host file system.";
                cmd.AddName("savebasic");

                var fileName = cmd.Argument("filename", "Name of the Basic file.")
                    .IsRequired();

                cmd.OnValidationError((ValidationResult validationResult) =>
                {
                    return monitor.WriteValidationError(validationResult);
                });

                cmd.OnExecute(() =>
                {
                    ushort startAddressValue = 0x0801;
                    var endAddressValue = (ushort)(monitor.Mem.FetchWord(0x2d) - 1);
                    monitor.SaveBinary(fileName.Value, startAddressValue, endAddressValue, addFileHeaderWithLoadAddress: true);

                    monitor.WriteOutput($"Basic program saved to {fileName.Value}");
                    return (int)CommandResult.Ok;
                });
            });
        }

        public void Reset(MonitorBase monitor)
        {
        }

        public void AfterLoadBasic(MonitorBase monitor, ushort loadedAtAddress, ushort fileLength)
        {
            monitor.WriteOutput($"Basic program loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");

            // The following memory locations are pointers to where Basic expects variables to be stored.
            // The address should be one byte after the Basic program end address after it's been loaded
            // VARTAB $002D-$002E   Pointer to the Start of the BASIC Variable Storage Area
            // ARYTAB $002F-$0030   Pointer to the Start of the BASIC Array Storage Area
            // STREND $0031-$0032   Pointer to End of the BASIC Array Storage Area (+1), and the Start of Free RAM
            // Ref: https://www.pagetable.com/c64ref/c64mem/
            ushort varStartAddress = (ushort)(loadedAtAddress + fileLength + 1);
            monitor.Mem.WriteWord(0x2d, varStartAddress);
            monitor.Mem.WriteWord(0x2f, varStartAddress);
            monitor.Mem.WriteWord(0x31, varStartAddress);
        }
    }
}
