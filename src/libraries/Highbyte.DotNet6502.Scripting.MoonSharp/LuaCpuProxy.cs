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
/// </summary>
[MoonSharpUserData]
public class LuaCpuProxy
{
    private readonly CPU _cpu;

    internal LuaCpuProxy(CPU cpu) => _cpu = cpu;

    /// <summary>Program Counter (0–65535)</summary>
    public int pc => _cpu.PC;

    /// <summary>Accumulator register (0–255)</summary>
    public int a => _cpu.A;

    /// <summary>Index register X (0–255)</summary>
    public int x => _cpu.X;

    /// <summary>Index register Y (0–255)</summary>
    public int y => _cpu.Y;

    /// <summary>Stack Pointer (0–255)</summary>
    public int sp => _cpu.SP;

    /// <summary>Carry flag</summary>
    public bool carry => _cpu.ProcessorStatus.Carry;

    /// <summary>Zero flag</summary>
    public bool zero => _cpu.ProcessorStatus.Zero;

    /// <summary>Negative flag</summary>
    public bool negative => _cpu.ProcessorStatus.Negative;

    /// <summary>Overflow flag</summary>
    public bool overflow => _cpu.ProcessorStatus.Overflow;

    /// <summary>Interrupt disable flag</summary>
    public bool interrupt_disable => _cpu.ProcessorStatus.InterruptDisable;

    /// <summary>Decimal mode flag</summary>
    public bool decimal_mode => _cpu.ProcessorStatus.Decimal;
}
