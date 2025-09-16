namespace Highbyte.DotNet6502.Systems.Rendering;

/// <summary>
/// A common interface that may be used inside a ISystem implementation as a common way to be able to generate the source of how a frame is constructed.
/// Can typically be implemented in the same class that provides the IRenderSource implementation (IVideFrameProvider, IIVideoCommandStream, IPayloadProvider) in the ISystem Rendersource property.
/// </summary>
public interface IRenderGenerator
{
    //public void OnCycle();    // For possible future improvement if cycle exact rendering is implemented

    /// <summary>
    /// Optionally called after each CPU instruction
    /// </summary>
    public void OnAfterInstruction();

    /// <summary>
    /// Optionally called after all cycles/instructions for a frame have been processed.
    /// </summary>
    public void OnEndFrame();
}
