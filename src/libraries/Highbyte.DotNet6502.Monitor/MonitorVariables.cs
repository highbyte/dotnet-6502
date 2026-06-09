namespace Highbyte.DotNet6502.Monitor;

/// <summary>
/// Working variables for monitor
/// </summary>
public class MonitorVariables
{
    public ushort? LatestDisassemblyAddress { get; set; }
    public ushort? LatestMemoryDumpAddress { get; set; }

    /// <summary>
    /// Re-anchors the disassembly position so the next argument-less 'd' command starts at the
    /// current PC again. Called each time the monitor is shown, mirroring how VICE re-anchors
    /// disassembly to PC on every break/entry.
    /// <para>
    /// The memory dump pointer (<see cref="LatestMemoryDumpAddress"/>) is intentionally NOT reset
    /// here: like VICE, the 'm' command keeps its position across monitor sessions within the same
    /// emulator run, so the data region being browsed is not lost. It resets naturally when a new
    /// emulator run begins (a fresh monitor, and thus a fresh MonitorVariables, is created).
    /// </para>
    /// Mutates this instance in place so references captured by the monitor command handlers
    /// continue to observe the reset value.
    /// </summary>
    public void ResetDisassemblyAnchor()
    {
        LatestDisassemblyAddress = null;
    }
}