using System.Diagnostics;

namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// Internal storage of SID register values. The memory locations they are mapped are mostly write only, 
/// so this class will contain the current state for the SID registers internally for use in audio playback. 
/// </summary>
public class InternalSidState
{

    public Dictionary<ushort, byte> _sidRegValues = new Dictionary<ushort, byte>();

    public enum SidVoiceActionType { GateSet, GateClear, WaveFormSet, WaveFormClear, FrequencySet }
    public enum SidCommonActionType { VolumeSet }


    private Dictionary<byte, List<SidVoiceActionType>> _channelAudioActions = new()
    {
        {1, new List<SidVoiceActionType>() },
        {2, new List<SidVoiceActionType>() },
        {3, new List<SidVoiceActionType>() }
    };
    public Dictionary<byte, List<SidVoiceActionType>> ChannelAudioActions => _channelAudioActions;

    private List<SidCommonActionType> _commonAudioActions = new();
    public List<SidCommonActionType> CommonAudioActions => _commonAudioActions;

    public InternalSidState Clone()
    {
        return new InternalSidState
        {
            _sidRegValues = _sidRegValues.ToDictionary(entry => entry.Key, entry => entry.Value),
            _channelAudioActions = _channelAudioActions.ToDictionary(entry => entry.Key, entry => entry.Value),
            _commonAudioActions = new(_commonAudioActions)
        };
    }

    private void MagRegisterChangeToSidAudioAction(ushort address, byte value)
    {
        // Check voice control
        // - if gate is set or cleared (start/stop sound)
        // - if waveform is set or cleared (if cleared sound stops)
        if (address == SidAddr.VCREG1)
        {
            if (value.IsBitSet(0))
                _channelAudioActions[1].Add(SidVoiceActionType.GateSet);
            else
                _channelAudioActions[1].Add(SidVoiceActionType.GateClear);

            if ((value & 0b11110000) == 0)
                _channelAudioActions[1].Add(SidVoiceActionType.WaveFormClear);
            else
                _channelAudioActions[1].Add(SidVoiceActionType.WaveFormSet);
        }
        else if (address == SidAddr.VCREG2)
        {
            if (value.IsBitSet(0))
                _channelAudioActions[2].Add(SidVoiceActionType.GateSet);
            else
                _channelAudioActions[2].Add(SidVoiceActionType.GateClear);

            if ((value & 0b11110000) == 0)
                _channelAudioActions[2].Add(SidVoiceActionType.WaveFormClear);
            else
                _channelAudioActions[2].Add(SidVoiceActionType.WaveFormSet);
        }
        else if (address == SidAddr.VCREG3)
        {
            if (value.IsBitSet(0))
                _channelAudioActions[3].Add(SidVoiceActionType.GateSet);
            else
                _channelAudioActions[3].Add(SidVoiceActionType.GateClear);

            if ((value & 0b11110000) == 0)
                _channelAudioActions[3].Add(SidVoiceActionType.WaveFormClear);
            else
                _channelAudioActions[3].Add(SidVoiceActionType.WaveFormSet);
        }

        // Check if voice frequency is set. If set during play, frequency will be changed.
        else if (address == SidAddr.FRELO1)
        {
            _channelAudioActions[1].Add(SidVoiceActionType.FrequencySet);
        }
        else if (address == SidAddr.FRELO2)
        {
            _channelAudioActions[2].Add(SidVoiceActionType.FrequencySet);
        }
        else if (address == SidAddr.FRELO3)
        {
            _channelAudioActions[3].Add(SidVoiceActionType.FrequencySet);
        }

        else if (address == SidAddr.FREHI1)
        {
            _channelAudioActions[1].Add(SidVoiceActionType.FrequencySet);
        }
        else if (address == SidAddr.FREHI2)
        {
            _channelAudioActions[2].Add(SidVoiceActionType.FrequencySet);
        }
        else if (address == SidAddr.FREHI3)
        {
            _channelAudioActions[3].Add(SidVoiceActionType.FrequencySet);
        }

        // Check if volume is set (common to all voices). If set during play, volume will be changed
        else if (address == SidAddr.SIGVOL)
        {
            _commonAudioActions.Add(SidCommonActionType.VolumeSet);
        }
    }

    public bool HasAudioChanged => _channelAudioActions.Sum(x => x.Value.Count) > 0
                                    || _commonAudioActions.Count > 0;
    public void ClearAudioChanged()
    {
        foreach (var voice in _channelAudioActions.Keys)
            _channelAudioActions[voice].Clear();
        _commonAudioActions.Clear();
    }

    public byte this[ushort index]
    {
        get
        {
            return GetSidRegValue(index);
        }
    }
    public byte GetSidRegValue(ushort address)
    {
        if (!_sidRegValues.ContainsKey(address))
            _sidRegValues.Add(address, 0);
        return _sidRegValues[address];
    }

    public void SetSidRegValue(ushort address, byte value)
    {
        _sidRegValues[address] = value;
        MagRegisterChangeToSidAudioAction(address, value);
    }
}
