using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;

public class C64SwiftLinkConfig
{
    [JsonIgnore]
    private Action? _markDirty;

    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            _markDirty?.Invoke();
        }
    }

    private C64CartridgeIOAddress _cartridgeIOAddress;
    public C64CartridgeIOAddress CartridgeIOAddress
    {
        get => _cartridgeIOAddress;
        set
        {
            _cartridgeIOAddress = value;
            _markDirty?.Invoke();
        }
    }

    private C64SwiftLinkInterruptMode _interruptMode;
    [JsonConverter(typeof(JsonStringEnumConverter<C64SwiftLinkInterruptMode>))]
    public C64SwiftLinkInterruptMode InterruptMode
    {
        get => _interruptMode;
        set
        {
            _interruptMode = value;
            _markDirty?.Invoke();
        }
    }

    private C64SwiftLinkReceiveMode _receiveMode;
    [JsonConverter(typeof(JsonStringEnumConverter<C64SwiftLinkReceiveMode>))]
    public C64SwiftLinkReceiveMode ReceiveMode
    {
        get => _receiveMode;
        set
        {
            _receiveMode = value;
            _markDirty?.Invoke();
        }
    }

    public C64SwiftLinkConfig()
    {
        _interruptMode = C64SwiftLinkInterruptMode.IRQ;
        _receiveMode = C64SwiftLinkReceiveMode.Compatible;
        _cartridgeIOAddress = C64CartridgeIOAddress.DE00;
        _enabled = false;
    }

    public void SetDirtyCallback(Action? markDirty) => _markDirty = markDirty;

    public bool IsValid(out List<string> validationErrors, string configPath = nameof(C64SwiftLinkConfig))
    {
        validationErrors = new List<string>();

        if (!Enum.IsDefined(CartridgeIOAddress))
            validationErrors.Add($"{configPath}.{nameof(CartridgeIOAddress)} has an invalid value.");
        if (!Enum.IsDefined(InterruptMode))
            validationErrors.Add($"{configPath}.{nameof(InterruptMode)} has an invalid value.");
        if (!Enum.IsDefined(ReceiveMode))
            validationErrors.Add($"{configPath}.{nameof(ReceiveMode)} has an invalid value.");

        return validationErrors.Count == 0;
    }

    public C64SwiftLinkConfig Clone()
    {
        var clone = (C64SwiftLinkConfig)MemberwiseClone();
        clone._markDirty = null;
        return clone;
    }
}
