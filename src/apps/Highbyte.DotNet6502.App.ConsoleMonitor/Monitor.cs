using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.App.ConsoleMonitor;

/// <summary>
/// A implementation of the 6502 Monitor in a native Console application.
/// </summary>
public class NativeConsoleMonitor : MonitorBase
{
    public NativeConsoleMonitor(
        SystemRunner systemRunner,
        MonitorConfig monitorConfig
        ) : base(systemRunner, monitorConfig)
    {
    }

    public override bool LoadBinary(string fileName, out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null, Action<MonitorBase, ushort, ushort>? afterLoadCallback = null)
    {
        if (!File.Exists(fileName))
        {
            WriteOutput($"File not found: {fileName}", MessageSeverity.Error);
            loadedAtAddress = 0;
            fileLength = 0;
            return false;
        }

        try
        {
            BinaryLoader.Load(
                Mem,
                fileName,
                out loadedAtAddress,
                out fileLength,
                forceLoadAddress);
            return true;
        }
        catch (Exception ex)
        {
            WriteOutput($"Load error: {ex.Message}", MessageSeverity.Error);
            loadedAtAddress = 0;
            fileLength = 0;
            return false;
        }

    }

    public override bool LoadBinary(out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null, Action<MonitorBase, ushort, ushort>? afterLoadCallback = null)
    {
        WriteOutput($"Loading file via file picker dialog not implemented.", MessageSeverity.Warning);
        loadedAtAddress = 0;
        fileLength = 0;
        return false;
    }

    public override void SaveBinary(string fileName, ushort startAddress, ushort endAddress, bool addFileHeaderWithLoadAddress)
    {
        try
        {
            BinarySaver.Save(
                Mem,
                fileName,
                startAddress,
                endAddress,
                addFileHeaderWithLoadAddress: addFileHeaderWithLoadAddress);
            WriteOutput($"Program saved to {fileName}");

        }
        catch (Exception ex)
        {
            WriteOutput($"Save error: {ex.Message}", MessageSeverity.Error);
        }
    }

    public override void WriteOutput(string message)
    {
        WriteOutput(message, MessageSeverity.Information);
    }

    public override void WriteOutput(string message, MessageSeverity severity)
    {
        switch (severity)
        {
            case MessageSeverity.Information:
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(message);
                break;
            case MessageSeverity.Warning:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(message);
                break;
            case MessageSeverity.Error:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(message);
                break;
            default:
                throw new NotImplementedException();
        }
    }
}
