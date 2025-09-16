
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
        _loop.FrameTick += OnFrameTick;

        _renderStat = _instrumentations.Add($"DrawCommands", new ElapsedMillisecondsTimedStat());
        _renderFps = _instrumentations.Add($"FPS", new PerSecondTimedStat());
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

    public void Dispose() => _loop.FrameTick -= OnFrameTick;

    public async ValueTask DisposeAsync()
    {
        Dispose();
    }
}
