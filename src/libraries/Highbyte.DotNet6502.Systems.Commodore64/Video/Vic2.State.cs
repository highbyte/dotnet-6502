namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

public partial class Vic2
{
    // Execution sequencers survive a frame boundary: sprite DMA can span the frame wrap,
    // and a CPU instruction can finish a few cycles into the next raster line. These are
    // chip state, not renderer caches. Explicit fixed-size fields avoid runtime-layout serialization.
    internal byte[] CaptureExecutionState()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        WriteSequencers(writer, includeLookupCache: true);
        foreach (var line in _lineDisplayStates)
            WriteLineState(writer, line);
        ((Vic2SpriteManager)SpriteManager).CaptureExecutionState(writer);
        return stream.ToArray();
    }

    internal void WriteCheckpoint(BinaryWriter writer)
    {
        writer.Write(CyclesConsumedCurrentVblank);
        writer.Write(CurrentVIC2Bank);
        writer.Write(Vic2IRQ.EnabledMask);
        writer.Write(Vic2IRQ.LatchedMask);
        writer.Write((int?)Vic2IRQ.ConfiguredIRQRasterLine ?? -1);
        WriteSequencers(writer, includeLookupCache: false);
        // At a frame boundary earlier lines belong to completed output. The current
        // and following line can still supply sprite data for in-flight fetches.
        int line = _currentRasterLineInternal == ushort.MaxValue ? 0 : _currentRasterLineInternal;
        int next = (line + 1) % _totalHeight;
        WriteLineState(writer, _lineDisplayStates[line]);
        WriteLineState(writer, _lineDisplayStates[next]);
        var sprites = (Vic2SpriteManager)SpriteManager;
        sprites.WriteCollisionState(writer);
        sprites.WriteCheckpointLine(writer, line);
        sprites.WriteCheckpointLine(writer, next);
    }

    private static void WriteLineState(BinaryWriter writer, Vic2LineDisplayState line)
    {
        writer.Write(line.DisplayState);
        writer.Write(line.VerticalBorder);
        writer.Write(line.VideoCounterBase);
        writer.Write(line.RowCounter);
    }

    private void WriteSequencers(BinaryWriter writer, bool includeLookupCache)
    {
        writer.Write(_advancedToBusCycle);
        writer.Write(_currentRasterLineInternal);
        writer.Write(_spriteEnableCache);
        writer.Write(_spriteYExpandCache);
        if (includeLookupCache)
        {
            // Bus-stall prediction can fill this lookup earlier after restoring a snapshot.
            // The result is fully determined by the Y registers and enable mask.
            writer.Write(_spriteYMatchLine);
            writer.Write(_spriteYMatchMask);
        }
        writer.Write(_spriteXJournalCount);
        writer.Write(SpriteFirstDataByteUnavailable);
        writer.Write(_spriteEventIndex);
        writer.Write(_nextSpriteEventOffset);
        writer.Write(SpriteDmaMask);
        writer.Write(SpriteDmaMaskPreviousLine);
        writer.Write(SpriteDisplayMask);
        writer.Write(_displayEnabledLatch);
        writer.Write(_displayState);
        writer.Write(_rasterIrqConditionMet);
        writer.Write(_videoCounterBase);
        writer.Write(_rowCounter);
        writer.Write(_displayStateAtLineStart);
        writer.Write(_rowCounterAtLineStart);
        writer.Write(_verticalBorderCurrent);
        writer.Write(_verticalBorderPending);
        writer.Write(_spriteCollisionsLatchedToPixel);
        writer.Write(_spriteShiftRegisterMask);
        writer.Write(_spriteXLowAtLineStart);
        writer.Write(_spriteMsbXAtLineStart);
        writer.Write(_spritePriorityAtLineStart);
        writer.Write(_spriteMultiColorAtLineStart);
        writer.Write(_spriteXExpandAtLineStart);
        writer.Write(_spriteSlotBusRecorded);
        writer.Write(_idleByteAtLineStart);
        writer.Write(_pendingBankCount);
        writer.Write(_lightPenTriggeredThisFrame);
        writer.Write(_spriteStartedInFirstCompare);
        writer.Write(_spriteMcBase);
        writer.Write(_spriteMc);
        writer.Write(_spriteFetchEnd);
        writer.Write(_spriteYCache);
        foreach (var value in _spriteExpandFlipFlop) writer.Write(value);
        foreach (var value in _spriteShiftRegister) writer.Write(value);
        writer.Write(_spriteSlotBusValue);
        writer.Write(_pendingBank);
        foreach (var value in _pendingBankFromBusCycle) writer.Write(value);
        for (int i = 0; i < _spriteXJournalCount; i++)
        {
            writer.Write(_spriteXJournalRegister[i]);
            writer.Write(_spriteXJournalValue[i]);
            writer.Write(_spriteXJournalPixel[i]);
        }
    }

    // A CIA port write selects the bank for the next cycle. Installing the current
    // bank must leave those pending transitions intact.
    internal void RestoreActiveBank(byte bank)
    {
        if (bank > 3)
            throw new InvalidDataException("Invalid VIC-II bank.");
        ApplyBank(bank);
    }

    internal void RestoreExecutionState(byte[] state)
    {
        using var stream = new MemoryStream(state, writable: false);
        using var reader = new BinaryReader(stream);
        _advancedToBusCycle = reader.ReadUInt64();
        _currentRasterLineInternal = reader.ReadUInt16();
        _spriteEnableCache = reader.ReadByte();
        _spriteYExpandCache = reader.ReadByte();
        _spriteYMatchLine = reader.ReadInt32();
        _spriteYMatchMask = reader.ReadByte();
        _spriteXJournalCount = reader.ReadInt32();
        SpriteFirstDataByteUnavailable = reader.ReadByte();
        _spriteEventIndex = reader.ReadInt32();
        _nextSpriteEventOffset = reader.ReadInt32();
        SpriteDmaMask = reader.ReadByte();
        SpriteDmaMaskPreviousLine = reader.ReadByte();
        SpriteDisplayMask = reader.ReadByte();
        _displayEnabledLatch = reader.ReadBoolean();
        _displayState = reader.ReadBoolean();
        _rasterIrqConditionMet = reader.ReadBoolean();
        _videoCounterBase = reader.ReadUInt16();
        _rowCounter = reader.ReadByte();
        _displayStateAtLineStart = reader.ReadBoolean();
        _rowCounterAtLineStart = reader.ReadByte();
        _verticalBorderCurrent = reader.ReadBoolean();
        _verticalBorderPending = reader.ReadBoolean();
        _spriteCollisionsLatchedToPixel = reader.ReadInt32();
        _spriteShiftRegisterMask = reader.ReadInt32();
        _spriteXLowAtLineStart = reader.ReadUInt64();
        _spriteMsbXAtLineStart = reader.ReadByte();
        _spritePriorityAtLineStart = reader.ReadByte();
        _spriteMultiColorAtLineStart = reader.ReadByte();
        _spriteXExpandAtLineStart = reader.ReadByte();
        _spriteSlotBusRecorded = reader.ReadBoolean();
        _idleByteAtLineStart = reader.ReadInt32();
        _pendingBankCount = reader.ReadInt32();
        _lightPenTriggeredThisFrame = reader.ReadBoolean();
        _spriteStartedInFirstCompare = reader.ReadByte();
        if (_spriteXJournalCount is < 0 or > SpriteXJournalCapacity || _pendingBankCount is < 0 or > 2
            || _spriteEventIndex < 0 || _spriteEventIndex > SpriteEventOffsets.Length
            || (_currentRasterLineInternal != ushort.MaxValue && _currentRasterLineInternal >= _totalHeight)
            || _rowCounter > 7 || _rowCounterAtLineStart > 7 || _videoCounterBase > 1023)
            throw new InvalidDataException("Invalid VIC-II sequencer state.");
        reader.BaseStream.ReadExactly(_spriteMcBase);
        reader.BaseStream.ReadExactly(_spriteMc);
        reader.BaseStream.ReadExactly(_spriteFetchEnd);
        reader.BaseStream.ReadExactly(_spriteYCache);
        for (int i = 0; i < _spriteExpandFlipFlop.Length; i++) _spriteExpandFlipFlop[i] = reader.ReadBoolean();
        for (int i = 0; i < _spriteShiftRegister.Length; i++) _spriteShiftRegister[i] = reader.ReadUInt32();
        reader.BaseStream.ReadExactly(_spriteSlotBusValue);
        reader.BaseStream.ReadExactly(_pendingBank);
        for (int i = 0; i < _pendingBankFromBusCycle.Length; i++) _pendingBankFromBusCycle[i] = reader.ReadUInt64();
        for (int i = 0; i < _spriteXJournalCount; i++)
        {
            _spriteXJournalRegister[i] = reader.ReadUInt16();
            _spriteXJournalValue[i] = reader.ReadByte();
            _spriteXJournalPixel[i] = reader.ReadInt32();
        }
        for (int i = 0; i < _lineDisplayStates.Length; i++)
            _lineDisplayStates[i] = new(reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadUInt16(), reader.ReadByte());
        ((Vic2SpriteManager)SpriteManager).RestoreExecutionState(reader);
        if (stream.Position != stream.Length)
            throw new InvalidDataException("Unexpected VIC-II sequencer data.");
        C64.CPU.RequestBusStallCheck();
    }

    /// <summary>
    /// Restores the live raster position saved in a snapshot: how far into the current frame the
    /// machine was. Without it a restored machine resumes at the top of a frame regardless of where
    /// it was interrupted, which moves every raster interrupt relative to the game's timing for the
    /// first frame — visible as a split-screen effect tearing on the frame after a load.
    ///
    /// <para>Only the cycle count is stored; the raster line is re-derived from it using the same
    /// expression <see cref="AdvanceRaster"/> uses, so the pair cannot be restored inconsistent with
    /// each other. The per-line side effects (raster IRQ, CIA timer stepping) are deliberately not
    /// replayed — they already ran on the machine that was captured.</para>
    /// </summary>
    internal void RestoreSnapshotRasterPosition(ulong cyclesConsumedCurrentVblank)
    {
        CyclesConsumedCurrentVblank = cyclesConsumedCurrentVblank < Vic2Model.CyclesPerFrame
            ? cyclesConsumedCurrentVblank
            : 0;

        var line = (ushort)(CyclesConsumedCurrentVblank / Vic2Model.CyclesPerLine);
        _currentRasterLineInternal = (ushort)Math.Clamp(line, 0, Vic2Model.TotalHeight - 1);
        var offset = (int)(CyclesConsumedCurrentVblank % Vic2Model.CyclesPerLine);
        _spriteEventIndex = 0;
        while (_spriteEventIndex < SpriteEventOffsets.Length && SpriteEventOffsets[_spriteEventIndex] <= offset)
            _spriteEventIndex++;
        _nextSpriteEventOffset = _spriteEventIndex < SpriteEventOffsets.Length ? SpriteEventOffsets[_spriteEventIndex] : int.MaxValue;
        RefreshSpriteRegisterCache();
        ResyncToBusCycle();
    }
}
