using Highbyte.DotNet6502.Systems.Oric.Hardware;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Oric.Snapshots;

/// <summary>Snapshot module for all live MOS 6522 register, timer, pin and interrupt state.</summary>
public sealed class OricViaSnapshotModule : ISnapshotModule
{
    public const string ModuleName = "oric-via";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var state = ((Oric)context.System).Via.GetSnapshotState();
        writer.WriteByte(state.PortA);
        writer.WriteByte(state.PortB);
        writer.WriteByte(state.DataDirectionA);
        writer.WriteByte(state.DataDirectionB);
        writer.WriteByte(state.ShiftRegister);
        writer.WriteByte(state.AuxiliaryControl);
        writer.WriteByte(state.PeripheralControl);
        writer.WriteByte(state.InterruptFlags);
        writer.WriteByte(state.InterruptEnable);
        writer.WriteUInt16(state.Timer1Counter);
        writer.WriteUInt16(state.Timer1Latch);
        writer.WriteUInt16(state.Timer2Counter);
        writer.WriteByte(state.Timer2LatchLow);
        writer.WriteBool(state.Timer1Running);
        writer.WriteBool(state.Timer2Running);
        writer.WriteBool(state.Ca1);
        writer.WriteBool(state.Cb1);
        writer.WriteBool(state.Ca2);
        writer.WriteBool(state.Cb2);
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var state = new Via6522SnapshotState(
            PortA: reader.ReadByte(),
            PortB: reader.ReadByte(),
            DataDirectionA: reader.ReadByte(),
            DataDirectionB: reader.ReadByte(),
            ShiftRegister: reader.ReadByte(),
            AuxiliaryControl: reader.ReadByte(),
            PeripheralControl: reader.ReadByte(),
            InterruptFlags: reader.ReadByte(),
            InterruptEnable: reader.ReadByte(),
            Timer1Counter: reader.ReadUInt16(),
            Timer1Latch: reader.ReadUInt16(),
            Timer2Counter: reader.ReadUInt16(),
            Timer2LatchLow: reader.ReadByte(),
            Timer1Running: reader.ReadBool(),
            Timer2Running: reader.ReadBool(),
            Ca1: reader.ReadBool(),
            Cb1: reader.ReadBool(),
            Ca2: reader.ReadBool(),
            Cb2: reader.ReadBool());
        ((Oric)context.System).Via.RestoreSnapshotState(state);
    }
}
