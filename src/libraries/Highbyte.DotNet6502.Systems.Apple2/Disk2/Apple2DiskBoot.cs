using Microsoft.Extensions.Logging;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Apple2.Disk2;

/// <summary>
/// Host-agnostic drive operations, so every host drives the same sequence.
///
/// Inserting a disk and booting from one are deliberately separate, because they are separate
/// things on the real machine. Applesoft has no disk commands at all — <c>CATALOG</c>,
/// <c>BLOAD</c> and friends come from DOS, which is itself software read off a diskette into
/// RAM. So the workflow is: boot a DOS disk once, then swap diskettes freely and use them from
/// the DOS prompt. (This is the opposite of a C64, where the 1541 carries its own DOS in ROM and
/// disk commands work from a cold machine.)
///
/// <see cref="BootAsync"/> is the emulated equivalent of typing <c>PR#6</c>: it re-runs the
/// slot-6 boot ROM. The one subtlety is that the Autostart ROM's slot scan only happens on a
/// <em>cold</em> start — a machine already at the BASIC prompt warm-starts straight back to the
/// prompt — so the power-up byte is invalidated first, as power-cycling the Apple would do.
/// </summary>
public static class Apple2DiskBoot
{
    /// <summary>
    /// Puts a DOS-ordered disk image in drive 1 without disturbing the running machine — the
    /// equivalent of swapping diskettes. Resident DOS picks up the new disk on its next access.
    /// </summary>
    /// <exception cref="InvalidOperationException">The current system is not an Apple II.</exception>
    /// <exception cref="InvalidDataException">The image is not a 140 KB DOS-ordered image.</exception>
    public static async Task InsertAsync(IHostApp hostApp, byte[] diskImageData, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(hostApp);
        ArgumentNullException.ThrowIfNull(diskImageData);

        var apple2 = await GetStartedApple2Async(hostApp);

        var wasRunning = hostApp.EmulatorState == EmulatorState.Running;
        if (wasRunning)
            hostApp.Pause();
        try
        {
            apple2.DiskController.InsertDiskImage(diskImageData);
            logger.LogInformation("Disk image inserted in drive 1.");
        }
        finally
        {
            if (wasRunning)
                await hostApp.Start();
        }
    }

    /// <summary>
    /// Boots from the disk currently in drive 1, like typing <c>PR#6</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The current system is not an Apple II, no Disk II boot ROM is configured, or the drive is
    /// empty.
    /// </exception>
    public static async Task BootAsync(IHostApp hostApp, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(hostApp);

        var apple2 = await GetStartedApple2Async(hostApp);

        if (apple2.DiskController.BootRom == null)
            throw new InvalidOperationException(
                "No Disk II boot ROM is configured. Add the 'disk2' ROM in the Apple II configuration to boot disk images.");
        if (!apple2.DiskController.IsDiskInserted)
            throw new InvalidOperationException("There is no disk in the drive to boot from.");

        var wasRunning = hostApp.EmulatorState == EmulatorState.Running;
        if (wasRunning)
            hostApp.Pause();
        try
        {
            apple2.InvalidatePowerUpByte();   // the slot scan only runs on a cold start
            apple2.Reset();
            logger.LogInformation("Machine cold-started to boot from slot 6.");
        }
        finally
        {
            if (wasRunning)
                await hostApp.Start();
        }
    }

    /// <summary>Inserts a disk and immediately boots from it.</summary>
    public static async Task InsertAndBootAsync(IHostApp hostApp, byte[] diskImageData, ILogger logger)
    {
        await InsertAsync(hostApp, diskImageData, logger);
        await BootAsync(hostApp, logger);
    }

    /// <summary>Ejects the disk. Leaves the machine running — as pulling a diskette would.</summary>
    public static void Eject(IHostApp hostApp, ILogger logger)
    {
        if (hostApp.CurrentRunningSystem is Apple2System apple2)
        {
            apple2.DiskController.RemoveDiskImage();
            logger.LogInformation("Disk image ejected.");
        }
    }

    private static async Task<Apple2System> GetStartedApple2Async(IHostApp hostApp)
    {
        if (hostApp.EmulatorState == EmulatorState.Uninitialized)
            await hostApp.Start();

        return hostApp.CurrentRunningSystem as Apple2System
            ?? throw new InvalidOperationException("The current system is not an Apple II.");
    }
}
