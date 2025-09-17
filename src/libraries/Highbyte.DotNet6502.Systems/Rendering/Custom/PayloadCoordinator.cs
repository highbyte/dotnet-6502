using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;

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

    private readonly Instrumentations _instrumentations;
    public Instrumentations Instrumentations => _instrumentations;
    private readonly ElapsedMillisecondsTimedStat _renderStat;
    private readonly PerSecondTimedStat _renderFps;

    public PayloadCoordinator(IPayloadProvider<TPayload> provider,
                              IRenderLoop loop,
                              ICustomRenderTarget<TPayload> target)
    {
        _instrumentations = new Instrumentations();
        _provider = provider; _loop = loop; _target = target;
        _loop.FrameTick += OnFrameTick;

        _renderStat = _instrumentations.Add($"DrawPayload", new ElapsedMillisecondsTimedStat());
        _renderFps = _instrumentations.Add($"FPS", new PerSecondTimedStat());
    }

    private async void OnFrameTick(object? s, TimeSpan t)
    {
        _renderFps.Update();
        _renderStat.Start();

        if (_provider.TryGetLatest(out var payload) && payload is not null)
            await _target.PresentAsync(payload);

        _renderStat.Stop();
    }

    public async ValueTask DisposeAsync()
    {
        _loop.FrameTick -= OnFrameTick;
        await _target.DisposeAsync();
    }
}
