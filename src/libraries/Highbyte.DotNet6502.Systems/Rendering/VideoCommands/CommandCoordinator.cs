
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;

namespace Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
public sealed class CommandCoordinator : IRenderCoordinator, IDisposable
{
    private readonly IVideoCommandStream _cmds;
    private readonly ICommandTarget _target;
    private readonly IRenderLoop _loop;

    private readonly Instrumentations _instrumentations;
    public Instrumentations Instrumentations => _instrumentations;
    private readonly ElapsedMillisecondsTimedStat _renderStat;
    private readonly PerSecondTimedStat _renderFps;

    public CommandCoordinator(IVideoCommandStream cmds, ICommandTarget target, IRenderLoop loop)
    {
        _instrumentations = new Instrumentations();
        _cmds = cmds; _target = target; _loop = loop;

        _renderFps = _instrumentations.Add($"FPS", new PerSecondTimedStat());

        if (_loop.Mode is RenderTriggerMode.HostFrameCallback)
        {
            _renderStat = _instrumentations.Add($"DrawCommands", new ElapsedMillisecondsTimedStat());

            // Host drives rendering: pull newest each host tick, or keep a retained one
            _loop.FrameTick += OnFrameTick;
        }
        else
        {
            _renderStat = _instrumentations.Add($"DrawCommands", new ElapsedMillisecondsTimedStat());

            // Manual invalidation: source will push frames; we ask host to redraw once per frame
            _cmds.FrameCompleted += OnFrameCompleted_RequestRedraw;
        }
    }

    private void OnFrameTick(object? s, TimeSpan t)
    {
        _renderFps.Update();
        _renderStat.Start();

        // Render exactly once per host frame
        _target.BeginFrame();
        foreach (var cmd in _cmds.DequeueAll())
            _target.Execute(cmd);
        _target.EndFrame();

        _renderStat.Stop();
    }

    private void OnFrameCompleted_RequestRedraw(object? sender, EventArgs e)
    {
        _loop.RequestRedraw();
    }

    public async ValueTask FlushIfDirtyAsync(CancellationToken ct = default)
    {
        _renderFps.Update();
        _renderStat.Start();

        try
        {
            // Render all commands for a frame
            _target.BeginFrame();
            foreach (var cmd in _cmds.DequeueAll())
                _target.Execute(cmd);
            _target.EndFrame();
        }
        finally
        {
        }

        _renderStat.Stop();
    }

    public void Dispose() => _loop.FrameTick -= OnFrameTick;

    public async ValueTask DisposeAsync()
    {
        Dispose();
    }
}
