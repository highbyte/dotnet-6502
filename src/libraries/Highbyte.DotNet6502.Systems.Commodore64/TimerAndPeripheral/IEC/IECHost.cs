namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.IEC;
public class IECHost
{
    private IECBus _iecBus;

    private DeviceLineState _setATNLine = DeviceLineState.NotHolding;
    private DeviceLineState _setCLKLine = DeviceLineState.NotHolding;
    private DeviceLineState _setDATALine = DeviceLineState.NotHolding;
    public DeviceLineState SetATNLine => _setATNLine;
    public DeviceLineState SetCLKLine => _setCLKLine;
    public DeviceLineState SetDATALine => _setDATALine;


    public void SetLines(DeviceLineState? setATNLine = null, DeviceLineState? setCLKLine = null, DeviceLineState? setDATALine = null)
    {
        _iecBus?.BeforeDeviceOrHostLineStateChanged();

        bool changed = false;

        if (setATNLine.HasValue && setATNLine.Value != _setATNLine)
        {
            _setATNLine = setATNLine.Value; changed = true;
        }

        if (setCLKLine.HasValue && setCLKLine.Value != _setCLKLine)
        {
            _setCLKLine = setCLKLine.Value; changed = true;
        }

        if (setDATALine.HasValue && setDATALine.Value != _setDATALine)
        {
            _setDATALine = setDATALine.Value; changed = true;
        }

        if (changed && _iecBus != null)
        {
            _iecBus.OnHostChangedState();
        }
    }

    internal void SetBus(IECBus iECBus)
    {
        if (_iecBus != null)
            throw new InvalidOperationException("IECHost is already set to a bus.");
        _iecBus = iECBus;
    }
}
