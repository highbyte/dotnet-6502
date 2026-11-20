using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Recording;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64;
using Highbyte.DotNet6502.Utils;
using MoonSharp.Interpreter;

namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// Exposes C64-specific system functions to Lua scripts.
/// Access from Lua via the global <c>c64</c> table:
/// <code>
/// if c64.basic_started() then ... end
/// local src = c64.get_basic_source()
/// c64.print_text("10 PRINT \"HELLO\"\n20 GOTO 10\n")
/// c64.load_d64("/path/to/disk.d64")
/// </code>
/// Methods are no-ops / return safe defaults before <see cref="SetC64"/> is called,
/// so scripts running before the emulator starts do not error.
/// </summary>
[MoonSharpUserData]
public class LuaC64Proxy
{
    private C64? _c64;

    private readonly IHostApp? _host;

    internal LuaC64Proxy(IHostApp? host = null) => _host = host;

    /// <summary>Called by the engine when a C64 system starts (or restarts after reset).</summary>
    internal void SetC64(C64? c64) => _c64 = c64;

    /// <summary>Read one byte from the C64 I/O backing storage without accessing the CPU bus.</summary>
    public DynValue read_io_storage(DynValue address)
    {
        if (address?.Type != DataType.Number || address.Number is < 0xd000 or > 0xdfff
            || address.Number != Math.Truncate(address.Number))
            throw new ScriptRuntimeException("c64.read_io_storage expects an integer address from 0xD000 through 0xDFFF.");

        var machine = _host == null ? _c64 : _host.CurrentRunningSystem as C64;
        if (machine == null)
            return DynValue.NewTuple(DynValue.Nil, DynValue.NewString("No C64 is loaded."));

        return DynValue.NewNumber(machine.ReadIOStorage((ushort)address.Number));
    }

    /// <summary>
    /// Returns true if C64 BASIC has completed initialization.
    /// Checks the TXTAB pointer at 0x002B–0x002C; it equals 0x0801 after BASIC init.
    /// </summary>
    public bool basic_started() => _c64?.HasBasicStarted() ?? false;

    /// <summary>
    /// Returns the current BASIC program in memory as a human-readable string.
    /// Returns an empty string if the system has not started or BASIC has not initialized.
    /// </summary>
    public string get_basic_source()
    {
        if (_c64 == null || !_c64.HasBasicStarted())
            return string.Empty;
        return _c64.BasicTokenParser.GetBasicText();
    }

    /// <summary>
    /// Queues <paramref name="text"/> into the C64 keyboard buffer so that BASIC
    /// tokenizes and stores the program exactly as if the user typed it.
    /// The emulator must be running in BASIC mode for the paste to be processed.
    /// Each line should end with a newline character (<c>\n</c>).
    /// No-op if the system has not started yet.
    /// </summary>
    /// <param name="text">BASIC source text with line numbers, e.g. "10 PRINT \"HI\"\n"</param>
    public void print_text(string text)
    {
        _c64?.TextPaste.Paste(text);
    }

    /// <summary>
    /// Parses the .d64 file at <paramref name="path"/> and inserts it into the first
    /// DiskDrive1541 attached to the running C64's IEC bus.
    /// The path is expanded cross-platform: <c>~</c> and <c>%HOME%</c> on Mac/Linux,
    /// <c>%USERPROFILE%</c> on Windows, and directory separators are normalised.
    /// Throws <see cref="InvalidOperationException"/> if no disk drive is present.
    /// No-op if the system has not started yet.
    /// </summary>
    /// <param name="path">Path to a .d64 disk image file. Supports <c>~</c>, <c>%HOME%</c>, and <c>%USERPROFILE%</c>.</param>
    public void load_d64(string path)
    {
        if (_c64 == null) return;
        RecordingSystemGuards.EnsureMutationAllowed(_c64);
        var expandedPath = PathHelper.ExpandOSEnvironmentVariables(path);
        var diskImage = D64Parser.ParseD64File(expandedPath);
        var diskDrive = _c64.IECBus?.Devices?.OfType<DiskDrive1541>().FirstOrDefault()
            ?? throw new InvalidOperationException("No DiskDrive1541 found in the running C64 system.");
        diskDrive.SetD64DiskImage(diskImage);
    }
}
