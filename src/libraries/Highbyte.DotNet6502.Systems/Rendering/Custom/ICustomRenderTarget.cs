
namespace Highbyte.DotNet6502.Systems.Rendering.Custom;

public interface ICustomRenderTarget<in TPayload> : IRenderTarget, IAsyncDisposable where TPayload : IRenderPayload
{
    public ValueTask PresentAsync(TPayload payload, CancellationToken ct = default);
}
