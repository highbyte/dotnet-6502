namespace Highbyte.DotNet6502.Systems.Recording;

/// <summary>Optional machine capability. Snapshot support alone does not imply replay support.</summary>
public interface IRecordingSystem
{
    IRecordingAdapter CreateRecordingAdapter();
}

/// <summary>
/// Captures resolved machine input, after all mapping/injection, and applies it without host
/// mapping. The payload may contain persistent state and ordered events. Implementations must
/// validate payloads without mutating the machine and hash canonical execution state only.
/// </summary>
public interface IRecordingAdapter
{
    string SystemName { get; }
    string Schema { get; }
    int SchemaVersion { get; }
    /// <summary>Identifies the ROMs and execution configuration independently of the producer build.</summary>
    string CompatibilityId { get; }
    /// <summary>Optional combined build/configuration hash used by version 1 recordings.</summary>
    string? LegacyCompatibilityId => null;
    double FramesPerSecond { get; }
    void ValidateSession();
    void ValidateMemoryInspection(ushort address, int length);
    byte[] CaptureInput();
    void ValidateInput(ReadOnlySpan<byte> input);
    bool CanRepeatInput(ReadOnlySpan<byte> input);
    void ApplyInput(ReadOnlySpan<byte> input);
    byte[] ComputeStateDigest();
    void ReleaseInput();
}
