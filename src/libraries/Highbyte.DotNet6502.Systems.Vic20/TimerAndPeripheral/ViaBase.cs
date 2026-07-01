namespace Highbyte.DotNet6502.Systems.Vic20.TimerAndPeripheral;

/// <summary>
/// Abstract base class for VIA 6522 chip instances.
/// Mirrors the structure of CiaBase in the C64 library — each concrete subclass
/// overrides <see cref="MapIOLocations"/> and adds port-specific logic (keyboard
/// matrix rows on VIA1, column strobe on VIA2).
/// </summary>
public abstract class ViaBase
{
    protected readonly Vic20 _vic20;
    protected readonly ViaIRQ _viaIRQ;
    protected readonly ViaTimer1 _timer1;

    // DDR shadow registers (stored in memory is fine for stubs, but we keep
    // local copies to avoid mapping conflicts with the chip registers).
    protected byte DDRA;
    protected byte DDRB;

    protected ViaBase(Vic20 vic20, ViaIRQ viaIRQ)
    {
        _vic20  = vic20;
        _viaIRQ = viaIRQ;
        _timer1 = new ViaTimer1(vic20, viaIRQ);
    }

    public virtual void ProcessTimers(ulong cyclesExecuted)
    {
        _timer1.ProcessTimer(cyclesExecuted);
    }

    /// <summary>
    /// Raise the CA1 interrupt (simulates the VIC-I raster pulse on VIA1).
    /// Call once per video frame from the emulator loop.
    /// </summary>
    public void TriggerCA1(CPU cpu)
    {
        _viaIRQ.SetCA1Condition();
        if (_viaIRQ.IsCA1Enabled)
            _viaIRQ.Trigger(cpu, "VIA_CA1");
    }

    /// <summary>Acknowledge (clear) the CA1 flag — called by Port A reads with handshake.</summary>
    protected void AcknowledgeCA1() => _viaIRQ.ClearCA1Condition();

    public abstract void MapIOLocations(Memory mem);

    // --- Snapshot support (consumed by the vic20-via snapshot module in the same assembly) ---
    internal byte SnapshotDdra { get => DDRA; set => DDRA = value; }
    internal byte SnapshotDdrb { get => DDRB; set => DDRB = value; }
    internal byte SnapshotAcr { get => _acr; set => _acr = value; }
    internal ViaTimer1 SnapshotTimer1 => _timer1;
    internal ViaIRQ SnapshotIrq => _viaIRQ;

    // ---------- Timer 1 read/write delegates (used by MapIOLocations) ----------

    protected byte  T1CLLoad (ushort _) => _timer1.ReadCounterLo();
    protected byte  T1CHLoad (ushort _) => _timer1.ReadCounterHi();   // side-effect: clears IFR T1
    protected byte  T1LLLoad (ushort _) => _timer1.ReadLatchLo();
    protected byte  T1LHLoad (ushort _) => _timer1.ReadLatchHi();
    protected void  T1LLStore(ushort _, byte v) => _timer1.WriteLatchLo(v);
    protected void  T1LHStore(ushort _, byte v) => _timer1.WriteLatchHiOnly(v); // latch only, no start
    protected void  T1CHStore(ushort _, byte v) => _timer1.WriteCounterHi(v);   // loads counter + starts

    // ---------- IFR / IER delegates ----------

    protected byte IFRLoad (ushort _)        => _viaIRQ.ReadIFR();
    protected void IFRStore(ushort _, byte v) => _viaIRQ.WriteIFR(v);
    protected byte IERLoad (ushort _)        => _viaIRQ.ReadIER();
    protected void IERStore(ushort _, byte v) => _viaIRQ.WriteIER(v);

    // ---------- DDR read/write ----------

    protected byte  DDRALoad (ushort _)        => DDRA;
    protected void  DDRAStore(ushort _, byte v) => DDRA = v;
    protected byte  DDRBLoad (ushort _)        => DDRB;
    protected void  DDRBStore(ushort _, byte v) => DDRB = v;

    // ---------- ACR read/write (bit 6 = T1 free-run vs one-shot) ----------

    private byte _acr;
    protected byte  ACRLoad (ushort _) => _acr;
    protected void  ACRStore(ushort _, byte v)
    {
        _acr = v;
        _timer1.SetFreeRunning((v & 0x40) != 0); // VIA 6522: bit 6 = 1 → free-running
    }

    // ---------- Stub for unimplemented registers (ignores reads/writes) ----------

    protected byte  StubLoad (ushort _)        => 0x00;
    protected void  StubStore(ushort _, byte v) { }
}
