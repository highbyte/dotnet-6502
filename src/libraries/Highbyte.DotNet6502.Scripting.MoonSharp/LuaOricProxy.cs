using Highbyte.DotNet6502.Systems.Oric.Input;
using Highbyte.DotNet6502.Systems.Oric.Tape;
using MoonSharp.Interpreter;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// Exposes Oric-specific system functions to Lua scripts through the global <c>oric</c> table.
/// Binary TAP data is represented as a dense, 1-based Lua table of byte values, matching
/// <c>file.read_bytes()</c> and <c>http.get_bytes()</c>.
/// </summary>
[MoonSharpUserData]
public sealed class LuaOricProxy
{
    private readonly Script _script;
    private OricMachine? _oric;

    internal LuaOricProxy(Script script)
    {
        _script = script;
    }

    /// <summary>Called whenever the running system changes.</summary>
    internal void SetOric(OricMachine? oric) => _oric = oric;

    /// <summary>Returns whether the Atmos BASIC prompt has initialized.</summary>
    public bool basic_started() => _oric?.IsSystemReady() ?? false;

    /// <summary>Returns the current tokenized BASIC program as source text.</summary>
    public string get_basic_source()
    {
        if (_oric == null || !_oric.IsSystemReady())
            return string.Empty;
        return _oric.BasicTokenParser.GetBasicText();
    }

    /// <summary>Queues text into the Oric keyboard input path.</summary>
    public void print_text(string text) => _oric?.TextPaste.Paste(text);

    /// <summary>
    /// Directly loads one file from a byte-level Oric TAP image. Lua file numbers are 1-based.
    /// Returns metadata for the loaded file.
    /// </summary>
    public Table load_tap(Table data, double file_number = 1, bool honor_autorun = true)
    {
        var oric = RequireOric("load_tap");
        if (!double.IsInteger(file_number) || file_number is < 1 or > int.MaxValue)
            throw new ScriptRuntimeException("oric.load_tap(): file_number must be a positive integer.");

        try
        {
            var file = oric.LoadTap(ToByteArray(data, "load_tap"), (int)file_number - 1, honor_autorun);
            return BuildFileTable(file);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or OverflowException)
        {
            throw new ScriptRuntimeException($"oric.load_tap(): {ex.Message}");
        }
    }

    /// <summary>Inserts and rewinds a byte-level Oric TAP image, returning its tape status.</summary>
    public Table insert_tape(Table data)
    {
        var oric = RequireOric("insert_tape");
        try
        {
            oric.InsertTape(ToByteArray(data, "insert_tape"));
            return BuildTapeStatusTable(oric);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or OverflowException)
        {
            throw new ScriptRuntimeException($"oric.insert_tape(): {ex.Message}");
        }
    }

    /// <summary>Rewinds the inserted tape to its first byte.</summary>
    public void rewind_tape() => RequireOric("rewind_tape").RewindTape();

    /// <summary>Ejects the current tape.</summary>
    public void eject_tape() => RequireOric("eject_tape").EjectTape();

    /// <summary>Returns the current tape transport state and parsed file metadata.</summary>
    public Table tape_status() => BuildTapeStatusTable(RequireOric("tape_status"));

    /// <summary>Returns the configured Oric joystick interface: none, pase, or ijk.</summary>
    public string joystick_interface()
        => (_oric?.Joystick.Interface ?? OricJoystickInterface.None).ToString().ToLowerInvariant();

    private OricMachine RequireOric(string operation)
        => _oric ?? throw new ScriptRuntimeException($"oric.{operation}(): the current system is not an Oric.");

    private Table BuildTapeStatusTable(OricMachine oric)
    {
        var tape = oric.Tape;
        var files = new Table(_script);
        for (var index = 0; index < tape.Files.Count; index++)
            files[index + 1] = BuildFileTable(tape.Files[index]);

        var status = new Table(_script)
        {
            ["inserted"] = tape.IsInserted,
            ["position"] = tape.Position,
            ["length"] = tape.Length,
            ["at_end"] = tape.IsAtEnd,
            ["files"] = files,
        };
        return status;
    }

    private Table BuildFileTable(OricTapFile file)
    {
        return new Table(_script)
        {
            ["name"] = file.Name,
            ["type"] = file.IsBasic ? "basic" : file.IsMachineCode ? "machinecode" : $"${file.FileType:X2}",
            ["autorun"] = file.IsAutoRun,
            ["start"] = file.StartAddress,
            ["end"] = file.EndAddress,
        };
    }

    private static byte[] ToByteArray(Table data, string operation)
    {
        ArgumentNullException.ThrowIfNull(data);

        var result = new byte[data.Length];
        var valueCount = 0;
        foreach (var pair in data.Pairs)
        {
            if (pair.Key.Type != DataType.Number ||
                !double.IsInteger(pair.Key.Number) ||
                pair.Key.Number < 1 ||
                pair.Key.Number > result.Length)
            {
                throw new ScriptRuntimeException(
                    $"oric.{operation}(): data must be a dense 1-based byte table.");
            }

            var index = (int)pair.Key.Number;
            if (pair.Value.Type != DataType.Number ||
                !double.IsInteger(pair.Value.Number) ||
                pair.Value.Number is < 0 or > 255)
            {
                throw new ScriptRuntimeException(
                    $"oric.{operation}(): data[{index}] must be an integer from 0 to 255.");
            }

            result[index - 1] = (byte)pair.Value.Number;
            valueCount++;
        }

        if (valueCount != result.Length)
        {
            throw new ScriptRuntimeException(
                $"oric.{operation}(): data must be a dense 1-based byte table.");
        }
        return result;
    }
}
