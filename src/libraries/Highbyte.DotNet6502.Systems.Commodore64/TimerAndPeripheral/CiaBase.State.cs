using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;

public abstract partial class CiaBase
{
    // Exposes the live timer and IRQ state (not held in IO register storage) to the c64-cia
    // snapshot module, which lives in the same assembly.
    internal CiaTimer SnapshotTimerA => _timerA;
    internal CiaTimer SnapshotTimerB => _timerB;
    internal CiaIRQ SnapshotIrq => _ciaIRQ;

    internal void CaptureExecutionState(SnapshotModuleWriter writer)
    {
        writer.WriteUInt64(_advancedToBusCycle);
        writer.WriteUInt64(_lastInterruptControlReadBusCycle);
        writer.WriteBool(_lastInterruptControlReadTookEnabledFlag);
        writer.WriteBool(_ciaIRQ.IsConditionSet(IRQSource.Any));
        _timerA.CaptureExecutionState(writer);
        _timerB.CaptureExecutionState(writer);
    }

    internal void RestoreExecutionState(SnapshotModuleReader reader)
    {
        _advancedToBusCycle = reader.ReadUInt64();
        _lastInterruptControlReadBusCycle = reader.ReadUInt64();
        _lastInterruptControlReadTookEnabledFlag = reader.ReadBool();
        if (reader.ReadBool()) _ciaIRQ.ConditionSet(IRQSource.Any);
        else _ciaIRQ.ConditionClear(IRQSource.Any);
        _timerA.RestoreExecutionState(reader);
        _timerB.RestoreExecutionState(reader);
        RecomputeNextUnderflow();
    }
}
