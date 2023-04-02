using System.Diagnostics;

namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// Internal storage of SID register values. The memory locations they are mapped are mostly write only, 
/// so this class will contain the current state for the SID registers internally for use in audio playback. 
/// </summary>
public class InternalSidState
{
    private bool _audioChanged = false;
    public bool AudioChanged => _audioChanged;

    public Dictionary<ushort, byte> _sidRegValues = new Dictionary<ushort, byte>();

    private void SetAudioChanged()
    {
        _audioChanged = true;
    }

    public void ClearAudioChanged()
    {
        _audioChanged = false;
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
        if (address == SidAddr.FRELO1)
            Debug.WriteLine($"Change FRELO1 to: {value}");
        if (address == SidAddr.FREHI1)
            Debug.WriteLine($"Change FREHI1 to: {value}");

        _sidRegValues[address] = value;
        SetAudioChanged();
    }
}
