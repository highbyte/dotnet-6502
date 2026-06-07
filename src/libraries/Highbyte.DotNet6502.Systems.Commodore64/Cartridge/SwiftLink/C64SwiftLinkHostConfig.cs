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

    private string _tcpHost = "vme.compunet.live";
    public string TcpHost
    {
        get => _tcpHost;
        set
        {
            _tcpHost = value;
            _markDirty?.Invoke();
        }
    }

    private int _tcpPort = 6400;
    public int TcpPort
    {
        get => _tcpPort;
        set
        {
            _tcpPort = value;
            _markDirty?.Invoke();
        }
    }

    private bool _connectOnBoot = false;
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

    public bool IsValid(out List<string> validationErrors, string configPath = nameof(C64SwiftLinkHostConfig))
    {
        validationErrors = new List<string>();

        if (!Enum.IsDefined(TransportMode))
            validationErrors.Add($"{configPath}.{nameof(TransportMode)} has an invalid value.");

        if (TransportMode == C64SwiftLinkTransportMode.RawTcp)
        {
            if (string.IsNullOrWhiteSpace(TcpHost))
            {
                validationErrors.Add($"{configPath}.{nameof(TcpHost)} must be set for RawTcp mode.");
            }
            else if (Uri.CheckHostName(TcpHost.Trim()) == UriHostNameType.Unknown)
            {
                validationErrors.Add($"{configPath}.{nameof(TcpHost)} must be a valid host name or IP address.");
            }
        }
        else if (TransportMode == C64SwiftLinkTransportMode.HayesModem && ConnectOnBoot)
        {
            validationErrors.Add($"{configPath}.{nameof(ConnectOnBoot)} can only be enabled in RawTcp mode.");
        }

        if (TcpPort is < 1 or > 65535)
            validationErrors.Add($"{configPath}.{nameof(TcpPort)} must be between 1 and 65535.");

        return validationErrors.Count == 0;
    }

    public C64SwiftLinkHostConfig Clone()
    {
        var clone = (C64SwiftLinkHostConfig)MemberwiseClone();
        clone._markDirty = null;
        return clone;
    }
}
