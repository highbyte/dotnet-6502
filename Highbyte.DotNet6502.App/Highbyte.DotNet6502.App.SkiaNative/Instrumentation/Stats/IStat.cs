namespace Highbyte.DotNet6502.App.SkiaNative.Instrumentation.Stats
{
    // Credit to instrumentation/stat code to: https://github.com/davidwengier/Trains.NET
    public interface IStat
    {
        string GetDescription();

        bool ShouldShow();
    }
}