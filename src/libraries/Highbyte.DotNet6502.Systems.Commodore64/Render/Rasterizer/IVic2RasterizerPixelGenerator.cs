namespace Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;

/// <summary>
/// The part of <see cref="Vic2Rasterizer"/> that turns the VIC-II's state into pixels on the two
/// layers. Two implementations exist: <see cref="Vic2RasterizerSequencerPixelGenerator"/>, which
/// follows the chip's graphics sequencer pixel by pixel, and the legacy
/// <see cref="Vic2RasterizerUintPixelGenerator"/>, which draws by 8-pixel blocks with the display
/// registers sampled once per line and is kept, unchanged, as the faster fallback.
/// </summary>
public interface IVic2RasterizerPixelGenerator
{
    /// <summary>Draw the pixels of every cycle between where the generator last stopped and where the VIC-II is now.</summary>
    void CatchUpToVic2();

    /// <summary>See <see cref="IVic2CycleRenderer.LatchSpriteBackgroundCollisions"/>. A generator that does not resolve collisions from its pixels ignores it.</summary>
    void LatchSpriteBackgroundCollisions(int rasterLine, int upToPixel, int clearedToPixel) { }

    /// <summary>Finish the frame: the last line, the end-of-frame sprite pass, register resync.</summary>
    void OnEndFrame();
}
