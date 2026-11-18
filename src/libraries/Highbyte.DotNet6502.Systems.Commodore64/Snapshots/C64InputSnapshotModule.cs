using Highbyte.DotNet6502.Systems.Commodore64.Input;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Commodore64.Snapshots;

/// <summary>Resolved input and RESTORE edge history required for held keys across snapshot start.</summary>
public sealed class C64InputSnapshotModule : ISnapshotModule
{
    public string Name => "c64-input";
    public int Version => 1;
    public bool Required => true;
    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
        => writer.WriteBytes(C64ResolvedInput.Capture(((C64)context.System).Cia1));
    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var input = reader.ReadBytes() ?? throw new SnapshotException("Missing C64 input state.");
        C64ResolvedInput.Restore(((C64)context.System).Cia1, input);
    }
}
