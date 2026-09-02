using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.CycleEnginePrototype;

/// <summary>
/// The chosen design: the instruction stays atomic (one call executes it to completion, exactly like the
/// production handlers), but every cycle is a real bus access that carries the cycle number, and
/// the bus decides what a cycle costs. There is no resumable state and no per-cycle dispatch.
///
/// Two device-synchronization policies share the same handlers:
/// <list type="bullet">
/// <item><see cref="DeviceSync.PerCycle"/> ticks the devices once per bus access, i.e. it is the
/// per-cycle scheduler expressed through the bus.</item>
/// <item><see cref="DeviceSync.Lazy"/> lets the devices fall behind and catches them up in bulk at
/// instruction boundaries, plus exactly when a read reaches the next cycle at which BA will be low
/// (a watermark the devices can predict). This is the shape a C64 scheduler would use.</item>
/// </list>
/// Both produce the same cycle counts and the same device state; the tests hold them to each other.
/// </summary>
public sealed class AtomicStampedEngine : ICycleEngine
{
    public enum DeviceSync
    {
        PerCycle,
        Lazy,
    }

    private readonly CPU _cpu;
    private readonly Memory _mem;
    private readonly SystemStub _sys;
    private readonly DeviceSync _sync;
    private readonly bool _devices;
    private readonly bool _cmos;
    private ulong _cycle;
    private ulong _nextBaLowCycle;
    private int _baWatermarkVersion;

    public AtomicStampedEngine(CPU cpu, Memory mem, SystemStub system, DeviceSync sync, CpuFamily family, bool devicesEnabled)
    {
        _cpu = cpu;
        _mem = mem;
        _sys = system;
        _sync = sync;
        _cmos = family == CpuFamily.Cmos;
        _devices = devicesEnabled;
        RecomputeBaWatermark();
    }

    public string Name => _sync == DeviceSync.PerCycle ? "Atomic/PerCycle" : "Atomic/Lazy";
    public CPU Cpu => _cpu;
    public Memory Mem => _mem;
    public SystemStub System => _sys;
    public ulong Cycle => _cycle;

    public void FlushDevices()
    {
        if (_devices && _sync == DeviceSync.Lazy)
            CatchUpDevices(_cycle);
    }

    // ----- the bus -----

    private byte Read(ushort address)
    {
        if (_devices)
        {
            if (_sync == DeviceSync.PerCycle)
            {
                _sys.Tick();
                while (_sys.BaLow)
                {
                    _cycle++;       // stalled read cycle
                    _sys.Tick();
                }
            }
            else if (_cycle + 1 >= _nextBaLowCycle)
            {
                CatchUpDevices(_cycle + 1);
                while (_sys.BaLow)
                {
                    _cycle++;
                    _sys.Tick();
                }
                RecomputeBaWatermark();
            }
        }
        var value = _mem.Read(address);
        _cycle++;
        return value;
    }

    private void Write(ushort address, byte value)
    {
        if (_devices && _sync == DeviceSync.PerCycle)
            _sys.Tick();    // writes never stall on the 6510
        _mem.Write(address, value);
        _cycle++;
    }

    private void CatchUpDevices(ulong toMasterCycle)
    {
        var pending = (int)(toMasterCycle - _sys.MasterCycle);
        if (pending > 0)
            _sys.Advance(pending);
    }

    /// <summary>
    /// The watermark is the absolute master cycle at which BA next goes low. It stays valid until
    /// that cycle has passed or something other than time changed the device state, so it is only
    /// recomputed then; a prediction per instruction would cost more than the ticks it saves.
    /// </summary>
    private void RecomputeBaWatermark()
    {
        var until = _devices ? _sys.CyclesUntilBaLow() : int.MaxValue;
        _nextBaLowCycle = until == int.MaxValue ? ulong.MaxValue : _sys.MasterCycle + (ulong)until;
        _baWatermarkVersion = _sys.StateVersion;
    }

    private void EnsureBaWatermarkIsCurrent()
    {
        if (_baWatermarkVersion != _sys.StateVersion || _sys.MasterCycle >= _nextBaLowCycle)
            RecomputeBaWatermark();
    }

    // ----- execution -----

    public void RunInstruction()
    {
        if (_devices && _sync == DeviceSync.Lazy)
        {
            CatchUpDevices(_cycle);     // interrupt lines must reflect every cycle already spent
            EnsureBaWatermarkIsCurrent();
        }

        var interrupts = _cpu.CPUInterrupts;
        if (interrupts.NMIPending)
        {
            interrupts.ClearPendingNMI();
            InterruptEntry(CPU.NonMaskableIRQHandlerVector);
            return;
        }
        if (interrupts.IRQLineEnabled && !_cpu.ProcessorStatus.InterruptDisable)
        {
            interrupts.AcknowledgeAutoAcknowledgingIRQSources();
            InterruptEntry(CPU.BrkIRQHandlerVector);
            return;
        }

        var opcode = Read(_cpu.PC++);
        switch (opcode)
        {
            case SliceOpcodes.LdaImm:
                LoadA(Read(_cpu.PC++));
                break;

            case SliceOpcodes.LdaAbs:
            {
                var address = FetchAbsolute();
                LoadA(Read(address));
                break;
            }

            case SliceOpcodes.LdaAbsX:
            {
                var (effective, uncarried, crossed) = FetchAbsoluteIndexedX();
                var value = Read(uncarried);
                if (crossed)
                    value = Read(effective);
                LoadA(value);
                break;
            }

            case SliceOpcodes.StaAbsX:
            {
                var (effective, uncarried, _) = FetchAbsoluteIndexedX();
                Read(uncarried);
                Write(effective, _cpu.A);
                break;
            }

            case SliceOpcodes.IncAbs:
            {
                var address = FetchAbsolute();
                var value = Read(address);
                if (_cmos)
                    value = Read(address);
                else
                    Write(address, value);
                value++;
                BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(value, ref _cpu.ProcessorStatus);
                Write(address, value);
                break;
            }

            case SliceOpcodes.Bne:
            {
                var offset = (sbyte)Read(_cpu.PC++);
                if (_cpu.ProcessorStatus.Zero)
                    break;
                Read(_cpu.PC);
                var target = (ushort)(_cpu.PC + offset);
                if ((target & 0xFF00) != (_cpu.PC & 0xFF00))
                    Read((ushort)((_cpu.PC & 0xFF00) | (target & 0x00FF)));
                _cpu.PC = target;
                break;
            }

            case SliceOpcodes.Nop:
                Read(_cpu.PC);
                break;

            case SliceOpcodes.Pha:
                Read(_cpu.PC);
                Push(_cpu.A);
                break;

            case SliceOpcodes.Jsr:
            {
                var low = Read(_cpu.PC++);
                Read((ushort)(CPU.StackBaseAddress + _cpu.SP));
                Push((byte)(_cpu.PC >> 8));
                Push((byte)(_cpu.PC & 0xFF));
                var high = Read(_cpu.PC);
                _cpu.PC = (ushort)((high << 8) | low);
                break;
            }

            case SliceOpcodes.Rts:
            {
                Read(_cpu.PC);
                Read((ushort)(CPU.StackBaseAddress + _cpu.SP));
                var low = Pull();
                var high = Pull();
                _cpu.PC = (ushort)((high << 8) | low);
                Read(_cpu.PC);
                _cpu.PC++;
                break;
            }

            case SliceOpcodes.Rti:
            {
                Read(_cpu.PC);
                Read((ushort)(CPU.StackBaseAddress + _cpu.SP));
                var status = Pull();
                var low = Pull();
                var high = Pull();
                _cpu.ProcessorStatus.Value = status;
                _cpu.ProcessorStatus.Break = false;
                _cpu.ProcessorStatus.Unused = true;
                _cpu.PC = (ushort)((high << 8) | low);
                break;
            }

            default:
                throw new InvalidOperationException($"Opcode ${opcode:X2} is not part of the prototype slice.");
        }
    }

    private void InterruptEntry(ushort vector)
    {
        Read(_cpu.PC);
        Read(_cpu.PC);
        Push((byte)(_cpu.PC >> 8));
        Push((byte)(_cpu.PC & 0xFF));
        var status = _cpu.ProcessorStatus;
        status.Break = false;
        status.Unused = true;
        Push(status.Value);
        _cpu.ProcessorStatus.InterruptDisable = true;
        if (_cmos)
            _cpu.ProcessorStatus.Decimal = false;
        var low = Read(vector);
        var high = Read((ushort)(vector + 1));
        _cpu.PC = (ushort)((high << 8) | low);
    }

    private ushort FetchAbsolute()
    {
        var low = Read(_cpu.PC++);
        var high = Read(_cpu.PC++);
        return (ushort)((high << 8) | low);
    }

    private (ushort Effective, ushort Uncarried, bool Crossed) FetchAbsoluteIndexedX()
    {
        var low = Read(_cpu.PC++);
        var high = Read(_cpu.PC++);
        var baseAddress = (ushort)((high << 8) | low);
        var crossed = low + _cpu.X > 0xFF;
        var uncarried = (ushort)((baseAddress & 0xFF00) | ((baseAddress + _cpu.X) & 0x00FF));
        return ((ushort)(baseAddress + _cpu.X), uncarried, crossed);
    }

    private void LoadA(byte value)
    {
        _cpu.A = value;
        BinaryArithmeticHelpers.SetFlagsAfterRegisterLoadIncDec(value, ref _cpu.ProcessorStatus);
    }

    private void Push(byte value)
    {
        Write((ushort)(CPU.StackBaseAddress + _cpu.SP), value);
        _cpu.SP--;
    }

    private byte Pull()
    {
        _cpu.SP++;
        return Read((ushort)(CPU.StackBaseAddress + _cpu.SP));
    }
}
