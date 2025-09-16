namespace Highbyte.DotNet6502.Systems.Rendering.Custom;

public interface IPayloadProvider<TPayload> : IRenderSource where TPayload : IRenderPayload
{
    public event EventHandler<TPayload>? PayloadProduced; // push model (per emu frame)
    public bool TryGetLatest(out TPayload? payload);      // pull model (host-driven)

    public Type PayloadType => typeof(TPayload);
}
