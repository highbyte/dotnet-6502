namespace Highbyte.DotNet6502.CycleEnginePrototype;

/// <summary>
/// The production executor driven the way the C64 drives it today: one atomic instruction, then
/// the devices advance by the returned cycle delta. The reference point every candidate's cost is
/// expressed against. Its bus trace lacks the implied-mode, branch and stack dummy reads the
/// cycle-stamped engine performs, so it is compared on final state and total cycles only.
/// </summary>
public sealed class LegacyEngine : ICycleEngine
{
    private readonly CPU _cpu;
    private readonly Memory _mem;
    private readonly SystemStub _sys;
    private readonly bool _devices;
    private ulong _cycle;

    public LegacyEngine(CPU cpu, Memory mem, SystemStub system, bool devicesEnabled)
    {
        _cpu = cpu;
        _mem = mem;
        _sys = system;
        _devices = devicesEnabled;
    }

    public string Name => "Legacy";
    public CPU Cpu => _cpu;
    public Memory Mem => _mem;
    public SystemStub System => _sys;
    public ulong Cycle => _cycle;

    public void FlushDevices() { }

    public void RunInstruction()
    {
        var result = _cpu.ExecuteOneInstructionMinimal(_mem);
        _cycle += result.CyclesConsumed;
        if (_devices)
            _sys.Advance((int)result.CyclesConsumed);
    }
}
