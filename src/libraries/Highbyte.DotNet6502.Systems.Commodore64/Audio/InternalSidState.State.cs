using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Commodore64.Audio;

public partial class InternalSidState
{
    internal void CaptureBusState(SnapshotModuleWriter writer)
    {
        writer.WriteByte(_busLatchValue);
        writer.WriteUInt64(_busLatchCycle);
        writer.WriteBool(_busLatchLoaded);
    }

    internal void RestoreBusState(SnapshotModuleReader reader)
    {
        _busLatchValue = reader.ReadByte();
        _busLatchCycle = reader.ReadUInt64();
        _busLatchLoaded = reader.ReadBool();
    }

    /// <summary>
    /// Marks all writable SID registers ($D400-$D418) as changed. Used by the snapshot c64-sid
    /// module on restore: the register <em>values</em> are restored via the IO storage, but the
    /// audio providers are edge-triggered on register changes (see <see cref="IsAudioChanged"/> and
    /// <see cref="GetGateControl"/>). Flagging them changed makes the provider re-evaluate the
    /// restored state on its next update and restart any voices whose gate was already on at
    /// snapshot time (at the cost of a short attack transient).
    /// </summary>
    public void MarkAllRegistersChangedForSnapshotRestore()
    {
        for (ushort address = SidAddr.FRELO1; address <= SidAddr.SIGVOL; address++)
            _changedSidRegisters.Add(address);
    }
}
