using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Systems.Commodore64.Audio;

/// <summary>
/// Internal storage of SID register values. The memory locations they are mapped are mostly write only, 
/// so this class will contain the current state for the SID registers internally for use in audio playback. 
/// </summary>
public class InternalSidState
{
    private HashSet<ushort> _changedSidRegisters = new();

    private HashSet<ushort> _sidRegistersThatAlwaysAreConsideredChangeWhenWrittenTo = new()
    {
        SidAddr.SIGVOL,
        SidAddr.VCREG1,
        SidAddr.VCREG2,
        SidAddr.VCREG3
    };
    private readonly C64 _c64;

    public enum GateControl
    {
        StartAttackDecaySustain,
        StartRelease,
        StopAudio,
        None
    }

    public InternalSidState(C64 c64)
    {
        _c64 = c64;
    }

    /// <summary>
    /// Lazy getter for voice 3 waveform-output ($D41B). Set by the active audio provider during
    /// its Init; invoked on each memory read so the value reflects live SID state and unread
    /// registers cost nothing. Null when no audio provider supplies a value (e.g. the
    /// command-stream path) — reads then return 0.
    /// </summary>
    public Func<byte>? Osc3ReadbackProvider { get; set; }

    /// <summary>
    /// Lazy getter for voice 3 envelope-counter readback ($D41C). Same lifecycle as
    /// <see cref="Osc3ReadbackProvider"/>.
    /// </summary>
    public Func<byte>? Env3ReadbackProvider { get; set; }

    /// <summary>
    /// Receives every SID register write together with the CPU bus cycle it happened on. A sink
    /// that returns true has applied the write at that exact cycle, so it is not also recorded in
    /// the changed-register set drained at instruction end; false keeps the batched path. The
    /// sample-accurate audio provider installs itself here; the command-stream provider does not
    /// need it. Null when no consumer wants exact timing.
    /// </summary>
    public ISidRegisterWriteSink? RegisterWriteSink { get; set; }

    /// <summary>
    /// Cycles the SID's internal data bus keeps the last transferred byte before it decays to 0,
    /// as measured on the 6581 (the 8580 holds it far longer; a chip-model setting can make this
    /// per model). A read of a write-only register returns this latch, which is how software
    /// that does a read-modify-write on a SID register (a loader's <c>DEC $D418</c> noise) really
    /// behaves: the operation works on the last byte the chip saw.
    /// </summary>
    public const int BusLatchDecayCycles = 0x1D00;

    private byte _busLatchValue;
    private ulong _busLatchCycle;
    private bool _busLatchLoaded;

    /// <summary>Loads the chip's data-bus latch: every write, and every read of a readable register, does this.</summary>
    public void LatchBusValue(byte value)
    {
        _busLatchValue = value;
        _busLatchCycle = _c64.CPU.BusCycles;
        _busLatchLoaded = true;
    }

    /// <summary>What a read of a write-only register returns: the bus latch until it has decayed.</summary>
    public byte ReadWriteOnlyRegister()
    {
        if (!_busLatchLoaded)
            return 0;
        return _c64.CPU.BusCycles - _busLatchCycle < (ulong)BusLatchDecayCycles ? _busLatchValue : (byte)0;
    }

    /// <summary>
    /// Get volume 0-15.
    /// Common for all voices.
    /// </summary>
    /// <returns></returns>
    public int GetVolume() => (GetRawSidRegValue(SidAddr.SIGVOL) & 0b00001111);

    /// <summary>
    /// Returns true if volume register has been changed
    /// </summary>
    /// <param name="voice"></param>
    /// <returns></returns>
    public bool IsVolumeChanged => IsRawSidRegChanged(SidAddr.SIGVOL);

    /// <summary>
    /// Get frequency 0-65535.
    /// 
    /// The actual frequency is calculated as follows:
    /// 
    /// FREQUENCY=(REGISTER VALUE * CLOCK / 16777216)Hz
    /// 
    /// where CLOCK equals the system clock frequency, 1022730 for American (NTSC) systems, 985250 for European(PAL)
    /// </summary>
    /// <param name="voice"></param>
    /// <returns></returns>
    public ushort GetFrequency(byte voice)
    {
        var frelo = GetRawSidRegValue(SidAddr.VoiceRegisterMap[$"{SidVoiceRegisterType.FRELO}{voice}"]);
        var frehi = GetRawSidRegValue(SidAddr.VoiceRegisterMap[$"{SidVoiceRegisterType.FREHI}{voice}"]);
        return ByteHelpers.ToLittleEndianWord(frelo, frehi);
    }

    /// <summary>
    /// Returns true if either lo or hi frequency register for the specified voice has changed.
    /// </summary>
    /// <param name="voice"></param>
    /// <returns></returns>
    public bool IsFrequencyChanged(byte voice)
    {
        return IsRawSidRegChanged(SidAddr.VoiceRegisterMap[$"{SidVoiceRegisterType.FRELO}{voice}"])
                || IsRawSidRegChanged(SidAddr.VoiceRegisterMap[$"{SidVoiceRegisterType.FREHI}{voice}"]);
    }

    /// <summary>
    /// Get pulse width 0-4095.
    /// 
    /// The actual pulse width percentage is calculated as follows:
    /// 
    /// PULSE WIDTH=(REGISTER VALUE/40.95)%
    /// </summary>
    /// <param name="voice"></param>
    /// <returns></returns>
    public ushort GetPulseWidth(byte voice)
    {
        var pwlo = GetRawSidRegValue(SidAddr.VoiceRegisterMap[$"{SidVoiceRegisterType.PWLO}{voice}"]);
        var pwhi = (byte)(GetRawSidRegValue(SidAddr.VoiceRegisterMap[$"{SidVoiceRegisterType.PWHI}{voice}"]) & 0b00001111); // Only 4 bits of high byte is used
        return ByteHelpers.ToLittleEndianWord(pwlo, pwhi);
    }

    /// <summary>
    /// Returns true if either lo or hi pulse width register for the specified voice has changed.
    /// </summary>
    /// <param name="voice"></param>
    /// <returns></returns>
    public bool IsPulseWidthChanged(byte voice)
    {
        return IsRawSidRegChanged(SidAddr.VoiceRegisterMap[$"{SidVoiceRegisterType.PWLO}{voice}"])
                || IsRawSidRegChanged(SidAddr.VoiceRegisterMap[$"{SidVoiceRegisterType.PWHI}{voice}"]);
    }

    //public bool IsGateOn(byte voice)
    //{
    //    var reg = SidAddr.VoiceRegisterMap[$"{SidVoiceRegisterType.VCREG}{voice}"];
    //    var isGateOn = GetRawSidRegValue(reg).IsBitSet(0);
    //    return isGateOn;
    //}

    public GateControl GetGateControl(byte voice)
    {
        var reg = SidAddr.VoiceRegisterMap[$"{SidVoiceRegisterType.VCREG}{voice}"];
        if (!IsRawSidRegChanged(reg))
            return GateControl.None;

        var regValue = GetRawSidRegValue(reg);
        bool anyWaveFormSelected = (regValue & 0b11110000) > 0; // bits 4-7 contains wave form selection
        var isGateOn = regValue.IsBitSet(0);

        switch (anyWaveFormSelected, isGateOn)
        {
            case (true, true):
                return GateControl.StartAttackDecaySustain;
            case (true, false):
                return GateControl.StartRelease;
            case (false, true):
                return GateControl.None;
            case (false, false):
                return GateControl.StopAudio;
        }
    }


    public SidVoiceWaveForm GetWaveForm(byte voice)
    {
        var reg = SidAddr.VoiceRegisterMap[$"{SidVoiceRegisterType.VCREG}{voice}"];
        var vcregVal = GetRawSidRegValue(reg);
        if (vcregVal.IsBitSet(4))
            return SidVoiceWaveForm.Triangle;
        if (vcregVal.IsBitSet(5))
            return SidVoiceWaveForm.Sawtooth;
        if (vcregVal.IsBitSet(6))
            return SidVoiceWaveForm.Pulse;
        if (vcregVal.IsBitSet(7))
            return SidVoiceWaveForm.RandomNoise;
        return SidVoiceWaveForm.None;
    }

    /// <summary>
    /// Get attack duration in ms.
    /// </summary>
    /// <param name="voice"></param>
    /// <returns></returns>
    public int GetAttackDuration(byte voice) => Sid.AttackDurationMs[GetRawVoiceReg(voice, SidVoiceRegisterType.ATDCY) >> 4];

    /// <summary>
    /// Get decay duration in ms.
    /// </summary>
    /// <param name="voice"></param>
    /// <returns></returns>
    public int GetDecayDuration(byte voice) => Sid.DecayDurationMs[GetRawVoiceReg(voice, SidVoiceRegisterType.ATDCY) & 0b00001111];

    /// <summary>
    /// Sustain gain (volume) 0-15.
    /// </summary>
    /// <param name="voice"></param>
    /// <returns></returns>
    public int GetSustainGain(byte voice) => GetRawVoiceReg(voice, SidVoiceRegisterType.SUREL) >> 4;

    /// <summary>
    /// Get decay duration in ms.
    /// </summary>
    /// <param name="voice"></param>
    /// <returns></returns>
    public int GetReleaseDuration(byte voice) => Sid.DecayDurationMs[GetRawVoiceReg(voice, SidVoiceRegisterType.SUREL) & 0b00001111];

    /// <summary>
    /// Get raw SID register value for a specific voice and register type.
    /// </summary>
    /// <param name="voice"></param>
    /// <param name="regType"></param>
    /// <returns></returns>
    public byte GetRawVoiceReg(byte voice, SidVoiceRegisterType regType) => GetRawSidRegValue(SidAddr.VoiceRegisterMap[$"{regType}{voice}"]);


    public bool IsAudioChanged => _changedSidRegisters.Count > 0;
    public void ClearAudioChanged() => _changedSidRegisters.Clear();

    public byte this[ushort index] => GetRawSidRegValue(index);

    public byte GetRawSidRegValue(ushort address)
    {
        return _c64.ReadIOStorage(address);
        //if (!_sidRegValues.ContainsKey(address))
        //    _sidRegValues.Add(address, 0);
        //return _sidRegValues[address];
    }

    public bool IsRawSidRegChanged(ushort address) => _changedSidRegisters.Contains(address);

    /// <summary>
    /// Marks all writable SID registers ($D400-$D418) as changed. Used by the snapshot c64-sid
    /// module on restore: the register <em>values</em> are restored via the IO storage, but the
    /// audio providers are edge-triggered on register changes (see <see cref="IsAudioChanged"/> and
    /// <see cref="GetGateControl"/>). Flagging them changed makes the provider re-evaluate the
    /// restored state on its next update and restart any voices whose gate was already on at
    /// snapshot time (at the cost of a short attack transient).
    /// </summary>
    public void MarkAllRegistersChangedForSnapshotRestore()
    {
        for (ushort address = SidAddr.FRELO1; address <= SidAddr.SIGVOL; address++)
            _changedSidRegisters.Add(address);
    }

    public void SetSidRegValue(ushort address, byte value)
    {
        var changed = _sidRegistersThatAlwaysAreConsideredChangeWhenWrittenTo.Contains(address)
            || _c64.ReadIOStorage(address) != value;

        _c64.WriteIOStorage(address, value);
        LatchBusValue(value);

        // The write happens on the CPU's current bus cycle (the counter already includes it).
        if (RegisterWriteSink is not null && RegisterWriteSink.OnRegisterWrite(address, value, _c64.CPU.BusCycles))
            return;

        // Batched path: remembered until a provider drains the changed set at instruction end.
        if (changed)
            _changedSidRegisters.Add(address);
    }
}

/// <summary>Consumer of SID register writes at their exact bus cycle; see <see cref="InternalSidState.RegisterWriteSink"/>.</summary>
public interface ISidRegisterWriteSink
{
    /// <summary>
    /// Called for a write of <paramref name="value"/> to <paramref name="address"/> on CPU bus
    /// cycle <paramref name="busCycle"/>. Return true when the write has been applied at that
    /// cycle; return false to let it fall back to the batched changed-register path.
    /// </summary>
    bool OnRegisterWrite(ushort address, byte value, ulong busCycle);
}
