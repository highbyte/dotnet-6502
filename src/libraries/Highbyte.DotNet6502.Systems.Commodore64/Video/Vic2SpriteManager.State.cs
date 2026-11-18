namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

public partial class Vic2SpriteManager
{
    // Collision reads clear these latches, and the in-flight line/next-line sprite fetches
    // can survive a frame wrap. They affect CPU-visible IRQs as well as the displayed image.
    private void WriteCollisionState(BinaryWriter writer)
    {
        writer.Write(SpriteToSpriteCollisionStore);
        writer.Write(SpriteToSpriteCollisionIRQBlock);
        writer.Write(SpriteToBackgroundCollisionStore);
        writer.Write(SpriteToBackgroundCollisionIRQBlock);
        writer.Write(LineSpriteEnableMask);
        foreach (var value in LineSpriteY) writer.Write(value);
    }

    internal void CaptureExecutionState(BinaryWriter writer)
    {
        WriteCollisionState(writer);
        writer.Write(_lineSpriteDisplayMasks);
        writer.Write(_lineSpriteData);
        writer.Write(_lineSpriteRunMasks);
        writer.Write(_lineSpriteRunCount);
        foreach (var value in _lineSpriteRunStart) writer.Write(value);
        foreach (var value in _lineSpriteRunData) writer.Write(value);
        writer.Write(_lineSpriteRunLength);
        writer.Write(_lineSpriteRunStretch);
        writer.Write(_lineSpriteRunFlags);
        writer.Write(_lineSpriteRunPixels);
        writer.Write(_lineSpriteXExpand);
        writer.Write(_lineSpriteMultiColor);
    }

    internal void RestoreExecutionState(BinaryReader reader)
    {
        SpriteToSpriteCollisionStore = reader.ReadByte();
        SpriteToSpriteCollisionIRQBlock = reader.ReadBoolean();
        SpriteToBackgroundCollisionStore = reader.ReadByte();
        SpriteToBackgroundCollisionIRQBlock = reader.ReadBoolean();
        LineSpriteEnableMask = reader.ReadByte();
        for (int i = 0; i < LineSpriteY.Length; i++) LineSpriteY[i] = reader.ReadInt32();
        reader.BaseStream.ReadExactly(_lineSpriteDisplayMasks);
        reader.BaseStream.ReadExactly(_lineSpriteData);
        reader.BaseStream.ReadExactly(_lineSpriteRunMasks);
        reader.BaseStream.ReadExactly(_lineSpriteRunCount);
        for (int i = 0; i < _lineSpriteRunStart.Length; i++) _lineSpriteRunStart[i] = reader.ReadInt16();
        for (int i = 0; i < _lineSpriteRunData.Length; i++) _lineSpriteRunData[i] = reader.ReadUInt32();
        reader.BaseStream.ReadExactly(_lineSpriteRunLength);
        reader.BaseStream.ReadExactly(_lineSpriteRunStretch);
        reader.BaseStream.ReadExactly(_lineSpriteRunFlags);
        reader.BaseStream.ReadExactly(_lineSpriteRunPixels);
        reader.BaseStream.ReadExactly(_lineSpriteXExpand);
        reader.BaseStream.ReadExactly(_lineSpriteMultiColor);
        if (_lineSpriteRunCount.Any(value => value > MaxSpriteRunsPerLine)
            || _lineSpriteRunLength.Any(value => value > RunPixelCapacity)
            || _lineSpriteRunStretch.Any(value => value > RunPixelCapacity)
            || _lineSpriteRunFlags.Any(value => value > 15)
            || LineSpriteY.Any(value => value is < 0 or > 255))
            throw new InvalidDataException("Invalid VIC-II sprite execution state.");
    }
}
