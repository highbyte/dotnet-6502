namespace Highbyte.DotNet6502.Systems.Snapshots;

/// <summary>
/// Optional runtime-settings ("config") payload carried in a snapshot, separate from the emulated
/// machine state. See the "capturing runtime settings" section of the feature design doc.
///
/// <para>
/// Two opaque, owner-serialized JSON blocks:
/// <list type="bullet">
///   <item><see cref="SystemConfigJson"/> — a portable subset of the global <c>ISystemConfig</c>
///   (same type on every host), so it can be applied by any host app.</item>
///   <item><see cref="HostJson"/> — host-app-specific settings (general host-app settings plus the
///   per-backend host system config), tagged internally with the originating host app; applied only
///   by a host that recognizes them.</item>
/// </list>
/// The shared snapshot framework treats both as opaque strings and never interprets their contents;
/// each block is produced and consumed by its owner. This keeps the framework closed to change when
/// new settings are added.
/// </para>
/// </summary>
public sealed class SnapshotConfigContent
{
    public string? SystemConfigJson { get; init; }
    public string? HostJson { get; init; }

    public bool IsEmpty => string.IsNullOrEmpty(SystemConfigJson) && string.IsNullOrEmpty(HostJson);
}
