using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.SadConsole;

public class SadConsoleMonitor : MonitorBase
{
    private readonly MonitorConfig _monitorConfig;
    private readonly Action<string, MessageSeverity> _monitorOutputPrint;
    public const int MONITOR_CMD_HISTORY_VIEW_ROWS = 200;
    public List<(string Message, MessageSeverity Severity)> MonitorCmdHistory { get; private set; } = new();

    public SadConsoleMonitor(
        SystemRunner systemRunner,
        MonitorConfig monitorConfig,
        Action<string, MessageSeverity> monitorOutputPrint
        ) : base(systemRunner, monitorConfig)
    {
        _monitorConfig = monitorConfig;
        _monitorOutputPrint = monitorOutputPrint;
    }

    public override bool LoadBinary(string fileName, out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null, Action<MonitorBase, ushort, ushort>? afterLoadCallback = null)
    {
        if (!Path.IsPathFullyQualified(fileName))
            fileName = $"{_monitorConfig.DefaultDirectory}/{fileName}";

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

        // TODO: Opening native Dialog here leads to endless Enter keypress events being sent to inputtext field.
        //var dialogResult = Dialog.FileOpen(@"prg;*");
        //if (dialogResult.IsOk)
        //{
        //    var fileName = dialogResult.Path;
        //    BinaryLoader.Load(
        //        SystemRunner.System.Mem,
        //        fileName,
        //        out loadedAtAddress,
        //        out fileLength);
        //    return true;
        //}

        //loadedAtAddress = 0;
        //fileLength = 0;
        //return false;
    }

    public override void SaveBinary(string fileName, ushort startAddress, ushort endAddress, bool addFileHeaderWithLoadAddress)
    {
        if (!Path.IsPathFullyQualified(fileName))
            fileName = $"{_monitorConfig.DefaultDirectory}/{fileName}";

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
        MonitorCmdHistory.Add((message, severity));
        if (MonitorCmdHistory.Count > MONITOR_CMD_HISTORY_VIEW_ROWS)
            MonitorCmdHistory.RemoveAt(0);

        _monitorOutputPrint(message, severity);
    }
}
