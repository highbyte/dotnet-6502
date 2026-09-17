namespace Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;

/// <summary>
/// A render provider that draws the VIC-II's output cycle by cycle and can therefore be brought
/// up to the chip's position in the middle of a CPU instruction, not only when the instruction
/// has ended (<see cref="IRenderGenerator.OnAfterInstruction"/>). The C64 asks for that where the
/// order of the chip's fetches and the CPU's accesses within an instruction decides the picture:
/// while a read is held by the chip's bus request, and before a write into the chip's bank lands.
/// </summary>
public interface IVic2CycleRenderer
{
    /// <summary>Draw every cycle between where the renderer last stopped and where the VIC-II is now.</summary>
    void CatchUpToVic2();

    /// <summary>
    /// A read of the sprite-to-background collision register in the middle of a raster line:
    /// latch the collisions of the line's sprite pixels before <paramref name="upToPixel"/>
    /// (counted from the line's first cycle), which the read reports, and leave out of the line's
    /// remaining collisions those before <paramref name="clearedToPixel"/>, which it clears.
    /// </summary>
    void LatchSpriteBackgroundCollisions(int rasterLine, int upToPixel, int clearedToPixel);
}
