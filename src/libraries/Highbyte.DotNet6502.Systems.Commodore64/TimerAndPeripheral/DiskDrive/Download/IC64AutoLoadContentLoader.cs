namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;

public interface IC64AutoLoadContentLoader
{
    Task LoadAsync(C64DownloadProgramInfo programInfo, C64 c64);
}
