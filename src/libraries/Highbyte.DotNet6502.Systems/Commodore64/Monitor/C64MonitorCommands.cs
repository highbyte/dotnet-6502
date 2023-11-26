using System.CommandLine;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Monitor.SystemSpecific;

namespace Highbyte.DotNet6502.Systems.Commodore64.Monitor;

/// <summary>
/// C64-specific monitor commands.
/// </summary>
public class C64MonitorCommands : ISystemMonitorCommands
{
    public void Configure(Command rootCommand, MonitorBase monitor)
    {
        rootCommand.AddCommand(BuildLoadBasicCommand(monitor));
        rootCommand.AddCommand(BuildLoadBasicManualCommand(monitor));
        rootCommand.AddCommand(BuildSaveBasicCommand(monitor));
    }

    private static Command BuildLoadBasicCommand(MonitorBase monitor)
    {
        var command = new Command("lb", "C64 - Load a CBM Basic 2.0 PRG file from file picker dialog.")
        {
        };
        command.AddAlias("loadbasic");
        command.AddAlias("loadbasicfilepicker");

        Func<Task<int>> handler = () =>
        {
            // Basic file should have a start address of 0801 stored as the two first bytes (little endian order, 01 08).
            var loaded = monitor.LoadBinary(out var loadedAtAddress, out var fileLength, null, AfterLoadBasic);
            if (!loaded)
            {
                // If file could not be loaded at this time, probably because a Web/WASM file picker dialog is asynchronus
                return Task.FromResult((int)CommandResult.Ok);
            }
            AfterLoadBasic(monitor, loadedAtAddress, fileLength);
            return Task.FromResult((int)CommandResult.Ok);
        };

        command.SetHandler(handler);
        return command;
    }

    private static Command BuildLoadBasicManualCommand(MonitorBase monitor)
    {
        var fileNameArg = new Argument<string>()
        {
            Name = "filename",
            Description = "Name of the binary file.",
            Arity = ArgumentArity.ExactlyOne
        };

        var command = new Command("llb", "C64 - Load a CBM Basic 2.0 PRG file from host file system.")
        {
            fileNameArg
        };
        command.AddAlias("loadbasicfile");

        Func<string, Task<int>> handler = (string fileName) =>
        {
            // Basic file should have a start address of 0801 stored as the two first bytes (little endian order, 01 08).
            bool loaded = monitor.LoadBinary(fileName, out var loadedAtAddress, out var fileLength);
            if (!loaded)
            {
                // If file could not be loaded, probably because it's not supported/implemented by the derived class.
                return Task.FromResult((int)CommandResult.Ok);
            }
            AfterLoadBasic(monitor, loadedAtAddress, fileLength);
            return Task.FromResult((int)CommandResult.Ok);
        };

        command.SetHandler(handler, fileNameArg);
        return command;
    }

    private static Command BuildSaveBasicCommand(MonitorBase monitor)
    {
        var fileNameArg = new Argument<string>()
        {
            Name = "filename",
            Description = "Name of the Basic file.",
            Arity = ArgumentArity.ExactlyOne
        };

        var command = new Command("sb", "C64 - Save a CBM Basic 2.0 PRG file to host file system.")
        {
            fileNameArg,
        };
        command.AddAlias("savebasic");

        Func<string, Task<int>> handler = (string fileName) =>
        {
            ushort startAddressValue = C64.BASIC_LOAD_ADDRESS;
            var endAddressValue = ((C64)monitor.System).GetBasicProgramEndAddress();
            monitor.SaveBinary(fileName, startAddressValue, endAddressValue, addFileHeaderWithLoadAddress: true);
            return Task.FromResult((int)CommandResult.Ok);
        };

        command.SetHandler(handler, fileNameArg);
        return command;
    }

    public void Reset(MonitorBase monitor)
    {
    }

    public static void AfterLoadBasic(MonitorBase monitor, ushort loadedAtAddress, ushort fileLength)
    {
        monitor.WriteOutput($"Basic program loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
        ((C64)monitor.System).InitBasicMemoryVariables(loadedAtAddress, fileLength);
    }
}
