using System.CommandLine;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Monitor.SystemSpecific;
using Highbyte.DotNet6502.Utils;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Apple2.Monitor;

/// <summary>
/// Apple II-specific monitor commands.
///
/// Applesoft BASIC files are handled as bare tokenized bytes with no file header (unlike C64
/// .prg files, whose first two bytes are the load address): the load address is always
/// <see cref="Apple2System.BASIC_LOAD_ADDRESS"/>, and after placing the bytes the Applesoft
/// zero-page pointers are initialised so RUN and LIST work.
/// </summary>
public class Apple2MonitorCommands : ISystemMonitorCommands
{
    public void Configure(Command rootCommand, MonitorBase monitor)
    {
        rootCommand.AddCommand(BuildLoadBasicCommand(monitor));
        rootCommand.AddCommand(BuildLoadBasicManualCommand(monitor));
        rootCommand.AddCommand(BuildSaveBasicCommand(monitor));
    }

    private static Command BuildLoadBasicCommand(MonitorBase monitor)
    {
        var command = new Command("lb", "Apple II - Load a tokenized Applesoft BASIC file (no header) from file picker dialog.")
        {
        };
        command.AddAlias("loadbasic");

        Func<Task<int>> handler = () =>
        {
            // Tokenized Applesoft files carry no load-address header; they always load at $0801.
            var loaded = monitor.LoadBinary(out var loadedAtAddress, out var fileLength,
                forceLoadAddress: Apple2System.BASIC_LOAD_ADDRESS, afterLoadCallback: AfterLoadBasic);
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
            Description = "Name of the tokenized Applesoft BASIC file.",
            Arity = ArgumentArity.ExactlyOne
        };

        var command = new Command("llb", "Apple II - Load a tokenized Applesoft BASIC file (no header) from host file system.")
        {
            fileNameArg
        };

        Func<string, Task<int>> handler = (string fileName) =>
        {
            bool loaded = monitor.LoadBinary(fileName, out var loadedAtAddress, out var fileLength,
                forceLoadAddress: Apple2System.BASIC_LOAD_ADDRESS);
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
            Description = "Name of the Applesoft BASIC file.",
            Arity = ArgumentArity.ExactlyOne
        };

        var command = new Command("sb", "Apple II - Save a tokenized Applesoft BASIC file (no header) to host file system.")
        {
            fileNameArg,
        };
        command.AddAlias("savebasic");

        Func<string, Task<int>> handler = (string fileName) =>
        {
            ushort startAddressValue = Apple2System.BASIC_LOAD_ADDRESS;
            var endAddressValue = ((Apple2System)monitor.System).GetBasicProgramEndAddress();
            monitor.SaveBinary(fileName, startAddressValue, endAddressValue, addFileHeaderWithLoadAddress: false);
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
        ((Apple2System)monitor.System).InitBasicMemoryVariables(loadedAtAddress, fileLength);
    }
}
