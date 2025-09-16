namespace Highbyte.DotNet6502.Systems.Rendering.Custom;
public interface IPayloadCoordinator : IRenderCoordinator
{
};

public sealed class PayloadCoordinator<TPayload> : IAsyncDisposable, IPayloadCoordinator
    where TPayload : IRenderPayload
{
    private readonly IPayloadProvider<TPayload> _provider;
    private readonly IRenderLoop _loop;
    private readonly ICustomRenderTarget<TPayload> _target;

    public PayloadCoordinator(IPayloadProvider<TPayload> provider,
                              IRenderLoop loop,
                              ICustomRenderTarget<TPayload> target)
    {
        _provider = provider; _loop = loop; _target = target;
        _loop.FrameTick += OnTick;
    }

    private async void OnTick(object? s, TimeSpan t)
    {
        if (_provider.TryGetLatest(out var payload) && payload is not null)
            await _target.PresentAsync(payload);
    }

    public async ValueTask DisposeAsync()
    {
        _loop.FrameTick -= OnTick;
        await _target.DisposeAsync();
    }
}
