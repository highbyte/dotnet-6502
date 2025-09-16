
namespace Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
public sealed class CommandCoordinator : IRenderCoordinator, IDisposable
{
    private readonly IVideoCommandStream _cmds;
    private readonly ICommandTarget _target;
    private readonly IRenderLoop _loop;

    public CommandCoordinator(IVideoCommandStream cmds, ICommandTarget target, IRenderLoop loop)
    {
        _cmds = cmds; _target = target; _loop = loop;
        _loop.FrameTick += OnFrameTick;
    }

    private void OnFrameTick(object? s, TimeSpan t)
    {
        // Coalesce: render exactly once per host frame
        _target.BeginFrame();
        foreach (var cmd in _cmds.DequeueAll())
            _target.Execute(cmd);
        _target.EndFrame();
    } 

    public void Dispose() => _loop.FrameTick -= OnFrameTick;

    public async ValueTask DisposeAsync()
    {
        Dispose();
    }
}
