namespace Highbyte.DotNet6502.CycleEnginePrototype;

public enum EngineKind
{
    Legacy,
    AtomicPerCycle,
    AtomicLazy,
}

public static class EngineFactory
{
    /// <summary>
    /// The two device-synchronization policies of the chosen cycle-stamped atomic design. Per-cycle
    /// sync is the reference oracle; lazy sync is the production policy. (Two resumable candidates,
    /// a micro-operation table and a flattened state machine, were also prototyped and measured
    /// here, then removed once the atomic design was chosen; see the repository history.)
    /// </summary>
    public static readonly EngineKind[] Candidates = [EngineKind.AtomicPerCycle, EngineKind.AtomicLazy];

    public static ICycleEngine Create(EngineKind kind, CPU cpu, Memory mem, SystemStub system, CpuFamily family = CpuFamily.Nmos, bool devicesEnabled = true)
        => kind switch
        {
            EngineKind.Legacy => new LegacyEngine(cpu, mem, system, devicesEnabled),
            EngineKind.AtomicPerCycle => new AtomicStampedEngine(cpu, mem, system, AtomicStampedEngine.DeviceSync.PerCycle, family, devicesEnabled),
            EngineKind.AtomicLazy => new AtomicStampedEngine(cpu, mem, system, AtomicStampedEngine.DeviceSync.Lazy, family, devicesEnabled),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
