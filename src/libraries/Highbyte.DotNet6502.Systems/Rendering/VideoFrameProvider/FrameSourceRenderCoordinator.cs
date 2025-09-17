using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;

namespace Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;

public sealed class FrameSourceRenderCoordinator : IRenderCoordinator
{
    private readonly IFrameSource _source;
    private readonly IRenderLoop _loop;
    private readonly IRenderFrameTarget _target;

    private readonly object _sync = new();
    private RenderFrame? _latestFrame; // retain last produced frame
    private readonly CancellationTokenSource _cts = new();

    private readonly Instrumentations _instrumentations;
    public Instrumentations Instrumentations => _instrumentations;
    private readonly ElapsedMillisecondsTimedStat _renderStat;
    private readonly PerSecondTimedStat _renderFps;

    public FrameSourceRenderCoordinator(
        IFrameSource source,
        IRenderLoop loop,
        IRenderFrameTarget target)
    {
        _instrumentations = new Instrumentations();
        _source = source;
        _loop = loop;
        _target = target;

        _renderFps = _instrumentations.Add($"FPS", new PerSecondTimedStat());

        if (_loop.Mode is RenderTriggerMode.HostFrameCallback)
        {
            _renderStat = _instrumentations.Add($"DrawFrame", new ElapsedMillisecondsTimedStat());

            // Host drives rendering: pull newest each host tick, or keep a retained one
            _loop.FrameTick += OnFrameTick;
        }
        else
        {
            _renderStat = _instrumentations.Add($"RequestRedraw", new ElapsedMillisecondsTimedStat());

            // Manual invalidation: source will push frames; we ask host to redraw once per frame
            _source.FrameProduced += OnFrameProduced_RequestRedraw;
        }
    }

    private void OnFrameTick(object? s, TimeSpan hostTime)
    {
        _renderFps.Update();
        _renderStat.Start();

        if (_source.TryGetLatestFrame(out var frame))
        {
            // Render on UI thread (we are usually already on it in host tick)
            if (frame != null)
                _ = PresentLatestAsync(frame);
        }
        else
        {
            // If no new frame, optionally re-present retained frame for VSync pacing, or do nothing.
        }

        _renderStat.Stop();
    }

    private void OnFrameProduced_RequestRedraw(object? s, RenderFrame frame)
    {
        _renderStat.Start();

        // Keep only the newest frame; dispose prior retained one.
        RenderFrame? old = null;
        lock (_sync)
        {
            old = Interlocked.Exchange(ref _latestFrame, frame);
        }
        if (old is not null) _ = old.DisposeAsync();

        // tell host “please render soon”; control’s Render() will call PresentLatestAsync
        _loop.RequestRedraw();

        _renderStat.Stop();
    }

    /// Call this from your host-control’s Render() (invalidate-driven) to flush the latest frame.
    public async ValueTask FlushIfDirtyAsync(CancellationToken ct = default)
    {
        RenderFrame? frame = null;
        lock (_sync)
        {
            frame = Interlocked.Exchange(ref _latestFrame, null);
        }
        if (frame is null) return;

        try
        {
            await _target.PresentAsync(frame, ct);
        }
        finally
        {
            await frame.DisposeAsync();
        }
    }

    private async Task PresentLatestAsync(RenderFrame frame)
    {
        try
        {
            await _target.PresentAsync(frame, _cts.Token);
        }
        finally
        {
            await frame.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _loop.FrameTick -= OnFrameTick;

        _source.FrameProduced -= OnFrameProduced_RequestRedraw;
        _cts.Cancel();

        if (_latestFrame is not null)
        {
            await _latestFrame.DisposeAsync();
            _latestFrame = null;
        }
        await _target.DisposeAsync();
        await _source.DisposeAsync();
        _cts.Dispose();
    }
}
