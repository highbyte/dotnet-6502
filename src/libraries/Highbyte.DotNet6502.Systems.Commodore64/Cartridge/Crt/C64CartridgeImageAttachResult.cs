namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge.Crt;

public sealed record C64CartridgeImageAttachResult(
    string CartridgeName,
    ushort HardwareType,
    byte Subtype,
    C64CartridgeLines Lines,
    int ChipCount,
    string? SourceName);
