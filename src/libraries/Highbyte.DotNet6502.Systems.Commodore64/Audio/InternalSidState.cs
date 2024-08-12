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

    public void SetSidRegValue(ushort address, byte value)
    {
        if (_sidRegistersThatAlwaysAreConsideredChangeWhenWrittenTo.Contains(address))
        {
            _changedSidRegisters.Add(address);
        }
        else
        {
            // Log sid register has changed since _changedSidRegisters last has been cleared.
            if (_c64.ReadIOStorage(address) != value)
                _changedSidRegisters.Add(address);
            //if (_sidRegValues.ContainsKey(address) && _sidRegValues[address] != value)
            //    _changedSidRegisters.Add(address);
        }

        _c64.WriteIOStorage(address, value);
        //_sidRegValues[address] = value;
    }
}
