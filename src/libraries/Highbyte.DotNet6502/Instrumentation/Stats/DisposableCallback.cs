namespace Highbyte.DotNet6502.Instrumentation.Stats;

// Credit to instrumentation/stat code to: https://github.com/davidwengier/Trains.NET
public class DisposableCallback : IDisposable
{
    public event EventHandler? Disposing;
    public bool Cont;

    public void Dispose()
    {
        Disposing?.Invoke(this, new DisposableCallbackEventArgs { Cont = Cont });
    }
}
public class DisposableCallbackEventArgs : EventArgs
{
    public bool Cont { get; set; }
}

