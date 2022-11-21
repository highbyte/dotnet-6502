namespace Highbyte.DotNet6502.App.SkiaNative.Instrumentation.Stats;

// Credit to instrumentation/stat code to: https://github.com/davidwengier/Trains.NET
public class DisposableCallback : IDisposable
{
    public event EventHandler? Disposing;
    public void Dispose()
    {
        Disposing?.Invoke(this, EventArgs.Empty);
    }
}