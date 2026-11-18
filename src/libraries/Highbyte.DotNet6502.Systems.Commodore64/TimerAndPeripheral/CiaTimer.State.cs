using Highbyte.DotNet6502.Systems.Snapshots;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

public partial class CiaTimer
{
    // The v1 prefix stores the visible counter and control. Version 2 appends the
    // in-flight load, stop and interrupt events through CaptureExecutionState.
    internal (ushort Latch, byte Control, ushort Current, bool Running) GetSnapshotState()
        => (_internalTimer_Latch, _timerControl, InternalTimer, StartBitSet);

    internal void CaptureExecutionState(SnapshotModuleWriter writer)
    {
        writer.WriteBool(_armed);
        writer.WriteUInt64(_countFrom);
        writer.WriteUInt64(_underflowAtBusCycle);
        writer.WriteUInt16(_counter);
        writer.WriteUInt64(_loadAt);
        writer.WriteUInt64(_stopAt);
        writer.WriteUInt64(_flagAt);
        writer.WriteUInt64(_irqAt);
        writer.WriteUInt64(_reloadedAt);
        writer.WriteBool(_flagLost);
    }

    internal void RestoreExecutionState(SnapshotModuleReader reader)
    {
        _armed = reader.ReadBool();
        _countFrom = reader.ReadUInt64();
        _underflowAtBusCycle = reader.ReadUInt64();
        _counter = reader.ReadUInt16();
        _loadAt = reader.ReadUInt64();
        _stopAt = reader.ReadUInt64();
        _flagAt = reader.ReadUInt64();
        _irqAt = reader.ReadUInt64();
        _reloadedAt = reader.ReadUInt64();
        _flagLost = reader.ReadBool();
    }

    internal void RestoreSnapshotState(ushort latch, byte control, ushort current, bool running)
    {
        _internalTimer_Latch = latch;
        _timerControl = control;
        if (running)
            _timerControl.SetBit(_timerControlStartBit);
        else
            _timerControl.ClearBit(_timerControlStartBit);
        _counter = current;
        _armed = false;
        _loadAt = ulong.MaxValue;
        _stopAt = ulong.MaxValue;
        _flagAt = ulong.MaxValue;
        _irqAt = ulong.MaxValue;
        _reloadedAt = ulong.MaxValue;
        _flagLost = false;
        Arm();
    }
}
