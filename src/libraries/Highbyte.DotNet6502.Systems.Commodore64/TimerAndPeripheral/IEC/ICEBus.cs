namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.IEC;

/// <summary>
/// The IECBus represents the IEC (serial bus) used for communication between the Commodore 64 and its serial peripherals, such as disk drives.
/// The C64 is assumed to be the master (host) on the bus.
/// </summary>
public class IECBus
{
    private readonly IECHost _iecHost;
    public IECHost Host => _iecHost;

    private readonly List<IIECDevice> _devices = new();
    public IReadOnlyList<IIECDevice> Devices => _devices.AsReadOnly();

    /// <summary>
    /// The state of the Attention line on the IEC bus.
    /// Only the host can hold (pull) the line low.
    /// </summary>
    public BusLineState ATNLineState => _iecHost.SetATNLine == DeviceLineState.NotHolding ? BusLineState.Released : BusLineState.Low;
    public BusLineState ATNLineStatePrevious { get; private set; } = BusLineState.Released;
    /// <summary>
    /// The state of the Clock line on the IEC bus.
    /// Any device may hold (pull) the line low. A line value becomes Low if any devices hold the line Low.
    /// </summary>
    public BusLineState CLKLineState
    {
        get
        {
            BusLineState lineState = _iecHost.SetCLKLine == DeviceLineState.Holding
                || _devices.Any(d => d.SetCLKLine == DeviceLineState.Holding)
                ? BusLineState.Low : BusLineState.Released;
            return lineState;
        }
    }
    public BusLineState CLKLineStatePrevious { get; private set; } = BusLineState.Released;


    /// <summary>
    /// The state of the Data line on the IEC bus.
    /// Any device may hold (pull) the line low. A line value becomes Low if any devices hold the line Low.
    /// </summary>
    public BusLineState DATALineState
    {
        get
        {
            BusLineState lineState = _iecHost.SetDATALine == DeviceLineState.Holding
                || _devices.Any(d => d.SetDATALine == DeviceLineState.Holding)
                ? BusLineState.Low : BusLineState.Released;
            return lineState;
        }
    }
    public BusLineState DATALineStatePrevious { get; private set; } = BusLineState.Released;


    public IECBus(IECHost iecHost)
    {
        _iecHost = iecHost;
        _iecHost.SetBus(this);
    }

    public void Attach(IIECDevice device)
    {
        if (_devices.Any(d => d.DeviceNumber == device.DeviceNumber))
            throw new InvalidOperationException($"A device with DeviceNumber {device.DeviceNumber} is already attached.");
        device.SetBus(this);
        _devices.Add(device);
    }

    public bool IsDeviceAttached(int deviceNumber)
    {
        return _devices.Any(d => d.DeviceNumber == deviceNumber);
    }

    public IIECDevice? GetDeviceByNumber(int deviceNumber)
    {
        return _devices.FirstOrDefault(d => d.DeviceNumber == deviceNumber);
    }

    public bool RemoveDeviceByNumber(int deviceNumber)
    {
        var device = _devices.FirstOrDefault(d => d.DeviceNumber == deviceNumber);
        if (device != null)
        {
            _devices.Remove(device);
            return true;
        }
        return false;
    }

    public void BeforeDeviceOrHostLineStateChanged()
    {
        ATNLineStatePrevious = ATNLineState;
        CLKLineStatePrevious = CLKLineState;
        DATALineStatePrevious = DATALineState;
    }

    public void OnHostChangedState()
    {
        foreach (var device in _devices)
        {
            device.OnBusChangedState();
        }
    }

    public void OnDevicesChangedState()
    {
    }

    public void TickDevices()
    {
        foreach (var device in _devices)
        {
            device.Tick();
        }
    }
}
