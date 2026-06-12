using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.App.Terminal;

/// <summary>
/// <see cref="MonitorBase"/> implementation for the terminal (TUI) host. Collects monitor output
/// into an in-memory ring buffer (shown by <see cref="MonitorDialog"/>) and loads/saves binaries via
/// the filesystem, rooted at the configured default directory when a path is not fully qualified.
/// </summary>
public sealed class TuiMonitor : MonitorBase
{
    private const int MaxOutputRows = 500;

    private readonly MonitorConfig _monitorConfig;
    private readonly Func<string, string?>? _filePicker;
    private readonly List<(string Message, MessageSeverity Severity)> _output = new();

    /// <summary>The accumulated monitor output (oldest first), capped at the most recent rows.</summary>
    public IReadOnlyList<(string Message, MessageSeverity Severity)> Output => _output;

    /// <param name="filePicker">
    /// Opens a host file picker (given a dialog title) and returns the chosen path, or null if the
    /// user cancelled. Used by the picker-based load commands ('l' / 'lb'). May be null (no picker).
    /// </param>
    public TuiMonitor(SystemRunner systemRunner, MonitorConfig monitorConfig, Func<string, string?>? filePicker = null)
        : base(systemRunner, monitorConfig)
    {
        _monitorConfig = monitorConfig;
        _filePicker = filePicker;
    }

    public override bool LoadBinary(
        string fileName, out ushort loadedAtAddress, out ushort fileLength,
        ushort? forceLoadAddress = null, Action<MonitorBase, ushort, ushort>? afterLoadCallback = null)
    {
        if (!Path.IsPathFullyQualified(fileName) && !string.IsNullOrEmpty(_monitorConfig.DefaultDirectory))
            fileName = Path.Combine(PathHelper.ExpandOSEnvironmentVariables(_monitorConfig.DefaultDirectory), fileName);

        if (!File.Exists(fileName))
        {
            WriteOutput($"File not found: {fileName}", MessageSeverity.Error);
            loadedAtAddress = 0;
            fileLength = 0;
            return false;
        }

        try
        {
            BinaryLoader.Load(Mem, fileName, out loadedAtAddress, out fileLength, forceLoadAddress);
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

    public override bool LoadBinary(
        out ushort loadedAtAddress, out ushort fileLength,
        ushort? forceLoadAddress = null, Action<MonitorBase, ushort, ushort>? afterLoadCallback = null)
    {
        loadedAtAddress = 0;
        fileLength = 0;

        if (_filePicker == null)
        {
            WriteOutput("File picker is not available here. Use the '...<file>' load variant instead.", MessageSeverity.Warning);
            return false;
        }

        var fileName = _filePicker("Load 6502 binary file");
        if (string.IsNullOrEmpty(fileName))
        {
            WriteOutput("Load cancelled.");
            return false;
        }

        // Returns true on success so the calling command finishes the load (sets PC for 'l', or runs
        // the system-specific after-load for 'lb'); we don't invoke afterLoadCallback ourselves here.
        return LoadBinary(fileName, out loadedAtAddress, out fileLength, forceLoadAddress, afterLoadCallback);
    }

    public override void SaveBinary(
        string fileName, ushort startAddress, ushort endAddress, bool addFileHeaderWithLoadAddress)
    {
        if (!Path.IsPathFullyQualified(fileName) && !string.IsNullOrEmpty(_monitorConfig.DefaultDirectory))
            fileName = Path.Combine(PathHelper.ExpandOSEnvironmentVariables(_monitorConfig.DefaultDirectory), fileName);

        try
        {
            BinarySaver.Save(Mem, fileName, startAddress, endAddress, addFileHeaderWithLoadAddress: addFileHeaderWithLoadAddress);
            WriteOutput($"Program saved to {fileName}");
        }
        catch (Exception ex)
        {
            WriteOutput($"Save error: {ex.Message}", MessageSeverity.Error);
        }
    }

    public override void WriteOutput(string message) => WriteOutput(message, MessageSeverity.Information);

    public override void WriteOutput(string message, MessageSeverity severity)
    {
        // Split embedded newlines so each line is one output row (the dialog renders one row per entry).
        foreach (var line in message.Replace("\r", string.Empty).Split('\n'))
            _output.Add((line, severity));

        if (_output.Count > MaxOutputRows)
            _output.RemoveRange(0, _output.Count - MaxOutputRows);
    }
}
