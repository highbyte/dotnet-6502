using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;

public class C64SwiftLinkHostConfig
{
    [JsonIgnore]
    private Action? _markDirty;

    private C64SwiftLinkTransportMode _transportMode;
    [JsonConverter(typeof(JsonStringEnumConverter<C64SwiftLinkTransportMode>))]
    public C64SwiftLinkTransportMode TransportMode
    {
        get => _transportMode;
        set
        {
            _transportMode = value;
            _markDirty?.Invoke();
        }
    }

    private string _tcpHost = "127.0.0.1";
    public string TcpHost
    {
        get => _tcpHost;
        set
        {
            _tcpHost = value;
            _markDirty?.Invoke();
        }
    }

    private int _tcpPort = 5000;
    public int TcpPort
    {
        get => _tcpPort;
        set
        {
            _tcpPort = value;
            _markDirty?.Invoke();
        }
    }

    private bool _connectOnBoot;
    public bool ConnectOnBoot
    {
        get => _connectOnBoot;
        set
        {
            _connectOnBoot = value;
            _markDirty?.Invoke();
        }
    }

    public C64SwiftLinkHostConfig()
    {
        _transportMode = C64SwiftLinkTransportMode.RawTcp;
    }

    public void SetDirtyCallback(Action? markDirty) => _markDirty = markDirty;

    public C64SwiftLinkHostConfig Clone()
    {
        var clone = (C64SwiftLinkHostConfig)MemberwiseClone();
        clone._markDirty = null;
        return clone;
    }
}
