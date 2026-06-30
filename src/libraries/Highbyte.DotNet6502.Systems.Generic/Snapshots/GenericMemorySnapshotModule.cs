using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Generic.Snapshots;

/// <summary>
/// Snapshot module for the Generic computer's memory. Captures the full memory view of the
/// active memory configuration (up to 64 KB) plus the memory configuration index.
///
/// <para>
/// Bytes are read/written through the <see cref="Memory"/> indexer rather than by cloning the
/// delegate-based reader/writer arrays (which the core deliberately does not expose — see the
/// note on <see cref="Memory"/>). On restore the system is expected to have been rebuilt
/// normally (so the memory map delegates already exist); this module only overwrites the bytes.
/// </para>
/// </summary>
public sealed class GenericMemorySnapshotModule : ISnapshotModule
{
    public const string ModuleName = "generic-memory";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var mem = context.System.Mem;

        writer.WriteInt32(mem.Size);
        writer.WriteInt32(mem.NumberOfConfigurations);
        writer.WriteInt32(mem.CurrentConfiguration);

        var bytes = new byte[mem.Size];
        for (int address = 0; address < mem.Size; address++)
            bytes[address] = mem[(ushort)address];
        writer.WriteBytes(bytes);
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var mem = context.System.Mem;

        int size = reader.ReadInt32();
        int numberOfConfigurations = reader.ReadInt32();
        int currentConfiguration = reader.ReadInt32();

        if (size != mem.Size)
            throw new SnapshotException(
                $"generic-memory: snapshot memory size {size} does not match target memory size {mem.Size}.");
        if (numberOfConfigurations != mem.NumberOfConfigurations)
            throw new SnapshotException(
                $"generic-memory: snapshot memory configuration count {numberOfConfigurations} does not match target {mem.NumberOfConfigurations}.");

        var bytes = reader.ReadBytes()
            ?? throw new SnapshotException("generic-memory: memory bytes were missing from the snapshot.");
        if (bytes.Length != size)
            throw new SnapshotException(
                $"generic-memory: snapshot memory byte count {bytes.Length} does not match declared size {size}.");

        // Restore into the configuration that was active when the snapshot was taken so writes
        // land in that configuration's backing arrays.
        if (currentConfiguration != mem.CurrentConfiguration)
            mem.SetMemoryConfiguration(currentConfiguration);

        for (int address = 0; address < size; address++)
            mem[(ushort)address] = bytes[address];
    }
}
