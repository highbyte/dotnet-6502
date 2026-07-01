using Highbyte.DotNet6502.Systems.Commodore64.Cartridge;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Commodore64.Snapshots;

/// <summary>
/// Snapshot module for an attached <c>.crt</c> cartridge. Embeds the original <c>.crt</c> image
/// bytes and, on restore, re-attaches the cartridge <em>without</em> a hard reset (so the machine
/// state restored by the other modules is preserved) and restores the cartridge's live mutable
/// state via <see cref="ISnapshotableCartridge"/>.
///
/// <para>
/// A cartridge's ROM/image content is reconstructed by re-attaching the embedded <c>.crt</c>;
/// only mutable state (bank/control registers, writable RAM, freeze flags) is serialized, by the
/// cartridge itself. Cartridges attached without a <c>.crt</c> image (e.g. the SwiftLink modem,
/// created from config) are not handled here — the rebuilt system recreates them from its config,
/// and live external connections are out of scope.
/// </para>
/// </summary>
public sealed class C64CartridgeSnapshotModule : ISnapshotModule
{
    public const string ModuleName = "c64-cartridge";
    public const string MediaId = "cartridge";
    public const string MediaKind = "crt";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var c64 = (C64)context.System;
        var crtBytes = c64.AttachedCrtImageBytes;
        var hasCrt = crtBytes != null;

        writer.WriteBool(hasCrt);
        if (!hasCrt)
            return;

        context.AddEmbeddedMedia(MediaId, MediaKind, c64.AttachedCartridgeImage?.SourceName, crtBytes!);
        writer.WriteString(c64.AttachedCartridgeImage?.SourceName);

        if (c64.CartridgeSlot.AttachedCartridge is ISnapshotableCartridge snapshotable)
        {
            writer.WriteBytes(snapshotable.CaptureSnapshotState());
        }
        else
        {
            writer.WriteBytes(Array.Empty<byte>());
            context.AddWarning(
                $"c64-cartridge: cartridge '{c64.CartridgeSlot.AttachedCartridge?.Name}' does not support snapshotting its live state; only the ROM image is restored.");
        }
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var c64 = (C64)context.System;
        var hasCrt = reader.ReadBool();
        if (!hasCrt)
            return;

        var sourceName = reader.ReadString();
        var liveState = reader.ReadBytes() ?? Array.Empty<byte>();

        if (!context.TryGetEmbeddedMedia(MediaId, out var crtBytes))
        {
            context.AddWarning("c64-cartridge: snapshot marked a cartridge attached but no embedded .crt image was found.");
            return;
        }

        // Re-attach from the embedded .crt without a hard reset, then restore the cartridge's live
        // state and re-derive the memory configuration (so cartridge lines affect the active bank).
        c64.AttachCrtImageForSnapshotRestore(crtBytes, sourceName);

        if (liveState.Length > 0 && c64.CartridgeSlot.AttachedCartridge is ISnapshotableCartridge snapshotable)
            snapshotable.RestoreSnapshotState(liveState);

        c64.ApplyCpuPortMemoryConfigurationFromSnapshot();
    }
}
