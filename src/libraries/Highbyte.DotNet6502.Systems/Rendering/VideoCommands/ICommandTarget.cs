namespace Highbyte.DotNet6502.Systems.Rendering.VideoCommands;

public interface ICommandTarget : IRenderTarget
{
    public void BeginFrame();
    public void Execute(IVideoCommand cmd);
    public void EndFrame();

    public ValueTask DisposeAsync();
}

