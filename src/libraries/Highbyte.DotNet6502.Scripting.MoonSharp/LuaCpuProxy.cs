using Highbyte.DotNet6502;
using MoonSharp.Interpreter;

namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// Exposes 6502 CPU state to Lua scripts as a read-only object.
/// Access from Lua via the global <c>cpu</c> table:
/// <code>
/// cpu.pc, cpu.a, cpu.x, cpu.y, cpu.sp
/// cpu.carry, cpu.zero, cpu.negative, cpu.overflow
/// </code>
/// Properties reflect the current CPU state at the time of access.
/// Returns safe defaults (0 / false) before <see cref="SetCpu"/> is called
/// so that scripts running before the emulator starts do not error.
/// </summary>
[MoonSharpUserData]
public class LuaCpuProxy
{
    private CPU? _cpu;

    internal LuaCpuProxy() { }

    /// <summary>Called by the engine when a system starts (or restarts after reset).</summary>
    internal void SetCpu(CPU cpu) => _cpu = cpu;

    /// <summary>Program Counter (0–65535)</summary>
    public int pc => _cpu?.PC ?? 0;

    /// <summary>Accumulator register (0–255)</summary>
    public int a => _cpu?.A ?? 0;

    /// <summary>Index register X (0–255)</summary>
    public int x => _cpu?.X ?? 0;

    /// <summary>Index register Y (0–255)</summary>
    public int y => _cpu?.Y ?? 0;

    /// <summary>Stack Pointer (0–255)</summary>
    public int sp => _cpu?.SP ?? 0;

    /// <summary>Carry flag</summary>
    public bool carry => _cpu?.ProcessorStatus.Carry ?? false;

    /// <summary>Zero flag</summary>
    public bool zero => _cpu?.ProcessorStatus.Zero ?? false;

    /// <summary>Negative flag</summary>
    public bool negative => _cpu?.ProcessorStatus.Negative ?? false;

    /// <summary>Overflow flag</summary>
    public bool overflow => _cpu?.ProcessorStatus.Overflow ?? false;

    /// <summary>Interrupt disable flag</summary>
    public bool interrupt_disable => _cpu?.ProcessorStatus.InterruptDisable ?? false;

    /// <summary>Decimal mode flag</summary>
    public bool decimal_mode => _cpu?.ProcessorStatus.Decimal ?? false;
}
