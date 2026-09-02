namespace Highbyte.DotNet6502.CycleEnginePrototype;

/// <summary>Which read-modify-write bus sequence the engine performs.</summary>
public enum CpuFamily
{
    /// <summary>NMOS 6502/6510: read, write the unmodified value back, write the result.</summary>
    Nmos,
    /// <summary>65C02: read, read again, write the result; Decimal cleared on interrupt entry.</summary>
    Cmos,
}

/// <summary>
/// A candidate CPU execution engine. All candidates share the same <see cref="CPU"/> register
/// file, the same <see cref="Memory"/>, and the same <see cref="SystemStub"/> devices, so their
/// bus traces, final state, and cycle counts are directly comparable.
/// </summary>
public interface ICycleEngine
{
    string Name { get; }
    CPU Cpu { get; }
    Memory Mem { get; }
    SystemStub System { get; }

    /// <summary>Completed CPU clock cycles, stalled cycles included.</summary>
    ulong Cycle { get; }

    /// <summary>
    /// Runs to the next instruction boundary. A pending NMI, or an IRQ with the interrupt-disable
    /// flag clear, is taken at the boundary instead of fetching an opcode, and its seven-cycle
    /// entry sequence counts as this call's work.
    /// </summary>
    void RunInstruction();

    /// <summary>
    /// Brings the devices up to <see cref="Cycle"/>. A no-op for engines that tick devices every
    /// cycle; the lazily synchronizing engine advances them here so device state can be compared.
    /// </summary>
    void FlushDevices();
}
