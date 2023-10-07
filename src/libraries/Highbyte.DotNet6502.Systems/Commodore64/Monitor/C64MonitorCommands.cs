using System.ComponentModel.DataAnnotations;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Monitor.Commands;
using Highbyte.DotNet6502.Monitor.SystemSpecific;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Systems.Commodore64.Monitor;

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
            cmd.Description = "C64 - Load a CBM Basic 2.0 PRG file from file picker dialog.";
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
            cmd.Description = "C64 - Load a CBM Basic 2.0 PRG file from host file system.";
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
                bool loaded = monitor.LoadBinary(fileName.Value!, out var loadedAtAddress, out var fileLength);
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
            cmd.Description = "C64 - Save a CBM Basic 2.0 PRG file to host file system.";
            cmd.AddName("savebasic");

            var fileName = cmd.Argument("filename", "Name of the Basic file.")
                .IsRequired();

            cmd.OnValidationError((ValidationResult validationResult) =>
            {
                return monitor.WriteValidationError(validationResult);
            });

            cmd.OnExecute(() =>
            {
                ushort startAddressValue = C64.BASIC_LOAD_ADDRESS;
                var endAddressValue = ((C64)monitor.System).GetBasicProgramEndAddress();
                monitor.SaveBinary(fileName.Value!, startAddressValue, endAddressValue, addFileHeaderWithLoadAddress: true);
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
        ((C64)monitor.System).InitBasicMemoryVariables(loadedAtAddress, fileLength);
    }
}
