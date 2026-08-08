using Highbyte.DotNet6502.Systems.Apple2.Peripherals;
using Highbyte.DotNet6502.Systems.Snapshots;

namespace Highbyte.DotNet6502.Systems.Apple2.Snapshots;

/// <summary>
/// Snapshot module for the Apple II mainboard: the 48 KB RAM, and the handful of latches behind the
/// soft-switch page — keyboard, display mode, speaker and game port. Paired with the shared
/// <c>cpu-6502</c> module.
///
/// <para>
/// This is one module rather than several because the machine has no chips to divide along. There
/// is no video chip with registers to re-derive state from, no timer, and no interrupt source: the
/// display mode is four write-only flip-flops, the keyboard is one byte, and the speaker is one
/// bit. All of it is decoded from the same page of addresses, so the mainboard is the natural seam,
/// and the Disk II card — which has removable media — is the other one.
/// </para>
///
/// <para>
/// Not captured: ROM (the system ROM, character generator and Disk II boot ROM all come from the
/// config when the machine is rebuilt) and the video output itself (the rasterizer reads memory
/// live, so restoring RAM and the display switches restores the picture).
/// </para>
/// </summary>
public sealed class Apple2CoreSnapshotModule : ISnapshotModule
{
    public const string ModuleName = "apple2-core";

    public string Name => ModuleName;
    public int Version => 1;
    public bool Required => true;

    public void Capture(SnapshotModuleWriter writer, SnapshotCaptureContext context)
    {
        var apple2 = (Apple2)context.System;

        writer.WriteBytes(apple2.SnapshotRam);

        // Keyboard latch (ASCII in bits 6-0, strobe in bit 7).
        writer.WriteByte(apple2.Keyboard.Latch);

        // Display soft switches.
        var switches = apple2.SoftSwitches;
        writer.WriteBool(switches.TextMode);
        writer.WriteBool(switches.MixedMode);
        writer.WriteBool(switches.Page2);
        writer.WriteBool(switches.HiRes);

        CaptureSpeaker(writer, apple2.Speaker);
        CaptureGamePort(writer, apple2.GamePort);
    }

    public void Restore(SnapshotModuleReader reader, SnapshotRestoreContext context)
    {
        var apple2 = (Apple2)context.System;

        var ram = reader.ReadBytes() ?? throw new SnapshotException("apple2-core: RAM bytes were missing.");
        if (ram.Length != apple2.SnapshotRam.Length)
            throw new SnapshotException(
                $"apple2-core: snapshot RAM size {ram.Length} does not match target {apple2.SnapshotRam.Length}.");
        // Copied into the existing array so the memory map, which holds a reference to it, keeps working.
        Array.Copy(ram, apple2.SnapshotRam, ram.Length);

        apple2.Keyboard.RestoreSnapshotLatch(reader.ReadByte());

        apple2.SoftSwitches.RestoreSnapshotDisplaySwitches(
            textMode: reader.ReadBool(),
            mixedMode: reader.ReadBool(),
            page2: reader.ReadBool(),
            hiRes: reader.ReadBool());

        RestoreSpeaker(reader, apple2.Speaker);
        RestoreGamePort(reader, apple2.GamePort);
    }

    private static void CaptureSpeaker(SnapshotModuleWriter writer, Apple2Speaker speaker)
    {
        writer.WriteBool(speaker.Level);
        writer.WriteUInt64(speaker.ToggleCount);
        writer.WriteUInt64(speaker.LastToggleCycle);
    }

    private static void RestoreSpeaker(SnapshotModuleReader reader, Apple2Speaker speaker)
    {
        var level = reader.ReadBool();
        var toggleCount = reader.ReadUInt64();
        var lastToggleCycle = reader.ReadUInt64();
        speaker.RestoreSnapshotState(level, toggleCount, lastToggleCycle);
    }

    private static void CaptureGamePort(SnapshotModuleWriter writer, Apple2GamePort gamePort)
    {
        for (var paddle = 0; paddle < Apple2GamePort.PaddleCount; paddle++)
            writer.WriteByte(gamePort.GetPaddlePosition(paddle));

        for (var button = 0; button < Apple2GamePort.ButtonCount; button++)
            writer.WriteBool(gamePort.IsButtonPressed(button));

        var triggeredAtCycle = gamePort.SnapshotTriggeredAtCycle;
        writer.WriteBool(triggeredAtCycle.HasValue);
        writer.WriteUInt64(triggeredAtCycle ?? 0);

        // Usage counters. Kept because they are how you tell whether a program touches the stick at
        // all, and a snapshot that reset them would make a restored session look like it never had.
        writer.WriteUInt64(gamePort.PaddleTriggerCount);
        for (var button = 0; button < Apple2GamePort.ButtonCount; button++)
            writer.WriteUInt64(gamePort.ButtonReadCounts[button]);
    }

    private static void RestoreGamePort(SnapshotModuleReader reader, Apple2GamePort gamePort)
    {
        for (var paddle = 0; paddle < Apple2GamePort.PaddleCount; paddle++)
            gamePort.SetPaddlePosition(paddle, reader.ReadByte());

        for (var button = 0; button < Apple2GamePort.ButtonCount; button++)
            gamePort.SetButton(button, reader.ReadBool());

        var hasTriggered = reader.ReadBool();
        var triggeredAtCycle = reader.ReadUInt64();
        var paddleTriggerCount = reader.ReadUInt64();
        gamePort.RestoreSnapshotState(hasTriggered ? triggeredAtCycle : null, paddleTriggerCount);

        for (var button = 0; button < Apple2GamePort.ButtonCount; button++)
            gamePort.ButtonReadCounts[button] = reader.ReadUInt64();
    }
}
