namespace Highbyte.DotNet6502.Systems.Instrumentation.Stats;

// Credit to instrumentation/stat code to: https://github.com/davidwengier/Trains.NET
public interface IStat
{
    string GetDescription();
    bool ShouldShow();

    /// <summary>
    /// Clears any accumulated/averaged measurement so the stat starts fresh.
    /// Default is a no-op for stats that don't accumulate.
    /// </summary>
    void ResetAverage() { }
}
