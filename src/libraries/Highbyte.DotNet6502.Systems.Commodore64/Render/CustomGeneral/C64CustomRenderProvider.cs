using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.Custom;
using Highbyte.DotNet6502.Systems.Utils;

namespace Highbyte.DotNet6502.Systems.Commodore64.Render.CustomPayload;

[DisplayName("Custom")]
[HelpText("Provides raw access to the C64 instance after each frame.\nCan be used to build custom renderers or for debugging purposes.")]
public sealed class C64CustomRenderProvider : IRenderProvider, IPayloadProvider<PayloadC64>
{
    public string Name => "C64CustomRenderProvider";

    private readonly C64 _c64;

    public event EventHandler<PayloadC64>? PayloadProduced;

    public C64CustomRenderProvider(C64 c64)
    {
        _c64 = c64;
    }

    public void OnAfterInstruction()
    {
    }

    public void OnEndFrame()
    {
        var payload = new PayloadC64(_c64);
        PayloadProduced?.Invoke(this, payload);
    }

    public bool TryGetLatest(out PayloadC64? payload)
    {
        payload = new PayloadC64(_c64);
        return payload is not null;
    }
}

public class PayloadC64 : IRenderPayload
{
    public C64 C64 { get; }
    public PayloadC64(C64 c64)
    {
        C64 = c64;
    }
}
