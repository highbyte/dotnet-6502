using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Apple2.Snapshots;

/// <summary>
/// Snapshot module for the language card: its 16 KB of RAM and the switch state that decides what
/// is visible at $D000-$FFFF.
///
/// <para>
/// Both halves are load-bearing. The RAM is where ProDOS itself lives once booted, so a snapshot
/// without it restores a machine whose operating system has been replaced by zeros. The switch
/// state decides whether the CPU is executing from the card or from ROM at the instant the snapshot
/// was taken — restore it wrong and the machine resumes running different code than it was.
/// </para>
///
/// <para>
/// Restores after <c>apple2-core</c>, and applies the memory configuration itself rather than
/// leaving it to the card's change event: the event is for live switch accesses, and a restore has
/// to end with the map in a known state whether or not the state it restored differs from the one
/// the freshly built machine started in.
/// </para>
/// </summary>
public sealed class Apple2LanguageCardSnapshotModule : ISnapshotModule
{
    public const string ModuleName = "apple2-languagecard";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var card = ((Apple2)context.System).LanguageCard;

        writer.WriteBytes(card.Ram);

        var (readRam, bank1Selected, writeEnabled, preWrite) = card.GetSnapshotState();
        writer.WriteBool(readRam);
        writer.WriteBool(bank1Selected);
        writer.WriteBool(writeEnabled);
        // The pre-write flip-flop is half of an in-flight unlock sequence. It costs one byte, and
        // without it a snapshot taken between the two switch reads resumes with the second read
        // silently failing to unlock the card.
        writer.WriteBool(preWrite);
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var apple2 = (Apple2)context.System;
        var card = apple2.LanguageCard;

        var ram = reader.ReadBytes()
            ?? throw new SnapshotException("apple2-languagecard: card RAM bytes were missing.");
        if (ram.Length != card.Ram.Length)
            throw new SnapshotException(
                $"apple2-languagecard: snapshot card RAM size {ram.Length} does not match target {card.Ram.Length}.");
        // Copied into the existing array, which every memory configuration's handlers close over.
        Array.Copy(ram, card.Ram, ram.Length);

        card.RestoreSnapshotState(
            readRam: reader.ReadBool(),
            bank1Selected: reader.ReadBool(),
            writeEnabled: reader.ReadBool(),
            preWrite: reader.ReadBool());

        if (apple2.LanguageCardEnabled)
        {
            apple2.Mem.SetMemoryConfiguration(card.MemoryConfiguration);
        }
        else if (card.ReadRam || card.WriteEnabled)
        {
            // Restoring a 64 KB capture into a machine configured without a card. The bytes are kept
            // (so re-enabling the card and reloading recovers them) but the address space has only
            // the one configuration, and switching to another would throw.
            context.AddWarning(
                "apple2-languagecard: the snapshot was taken with a language card switched in, but this " +
                "machine is configured without one — the card's mapping was not applied.");
        }
    }
}
