using Highbyte.DotNet6502.Systems.Oric.Render;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Oric.Snapshots;

/// <summary>Snapshot module for PAL raster position and renderer flash/attribute phase.</summary>
public sealed class OricRasterSnapshotModule : ISnapshotModule
{
    public const string ModuleName = "oric-raster";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var oric = (Oric)context.System;
        writer.WriteInt32(oric.RasterClock.RasterLine);
        writer.WriteInt32(oric.RasterClock.CycleInLine);
        writer.WriteUInt64(oric.RasterClock.FrameNumber);

        var rasterizer = oric.RenderProviders.OfType<OricRasterizer>().Single();
        var rasterizerState = rasterizer.GetSnapshotState();
        writer.WriteByte(rasterizerState.ScreenAttributes);
        writer.WriteInt32(rasterizerState.FrameCounter);
        writer.WriteBool(rasterizerState.ProgressiveFrameActive);

        var commandStream = oric.RenderProviders.OfType<OricVideoCommandStream>().Single();
        writer.WriteInt32(commandStream.SnapshotFrameCounter);
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var oric = (Oric)context.System;
        var rasterLine = reader.ReadInt32();
        var cycleInLine = reader.ReadInt32();
        var frameNumber = reader.ReadUInt64();
        if (rasterLine < 0 || rasterLine >= OricConfig.LinesPerFrame ||
            cycleInLine < 0 || cycleInLine >= OricConfig.CyclesPerLine)
        {
            throw new SnapshotException(
                $"oric-raster: invalid raster position line {rasterLine}, cycle {cycleInLine}.");
        }
        oric.RasterClock.RestoreSnapshotState(rasterLine, cycleInLine, frameNumber);

        var screenAttributes = reader.ReadByte();
        var rasterizerFrameCounter = reader.ReadInt32();
        var progressiveFrameActive = reader.ReadBool();
        var commandStreamFrameCounter = reader.ReadInt32();
        if ((screenAttributes & 0xf8) != 0)
            throw new SnapshotException($"oric-raster: invalid screen attributes ${screenAttributes:X2}.");

        oric.RenderProviders.OfType<OricRasterizer>().Single().RestoreSnapshotState(
            screenAttributes,
            rasterizerFrameCounter,
            progressiveFrameActive);
        oric.RenderProviders.OfType<OricVideoCommandStream>().Single().RestoreSnapshotState(
            commandStreamFrameCounter);
    }
}
