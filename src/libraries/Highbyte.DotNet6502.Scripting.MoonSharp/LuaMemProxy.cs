using Highbyte.DotNet6502;
using MoonSharp.Interpreter;

namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// Exposes 6502 memory read/write operations to Lua scripts.
/// Access from Lua via the global <c>mem</c> table:
/// <code>
/// local value = mem.read(0xD012)
/// mem.write(0x0400, 0x01)
/// </code>
/// Addresses and values are automatically masked to valid ranges (0–65535 and 0–255).
/// Returns 0 / no-ops before <see cref="SetMem"/> is called so that scripts running
/// before the emulator starts do not error.
/// </summary>
[MoonSharpUserData]
public class LuaMemProxy
{
    private Memory? _mem;

    internal LuaMemProxy() { }

    /// <summary>Called by the engine when a system starts (or restarts after reset).</summary>
    internal void SetMem(Memory mem) => _mem = mem;

    /// <summary>
    /// Reads a byte from the specified memory address.
    /// Returns 0 if the system has not started yet.
    /// </summary>
    /// <param name="address">Memory address (0–65535). Values outside the range are masked.</param>
    /// <returns>Byte value at the address (0–255).</returns>
    public int read(int address) =>
        _mem?.Read((ushort)(address & 0xFFFF)) ?? 0;

    /// <summary>
    /// Writes a byte to the specified memory address. No-op if the system has not started yet.
    /// </summary>
    /// <param name="address">Memory address (0–65535). Values outside the range are masked.</param>
    /// <param name="value">Byte value to write (0–255). Values outside the range are masked.</param>
    public void write(int address, int value)
    {
        _mem?.Write((ushort)(address & 0xFFFF), (byte)(value & 0xFF));
    }
}
