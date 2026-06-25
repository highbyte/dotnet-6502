namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge.Crt;

public sealed record C64CrtHeader(
    uint HeaderLength,
    ushort Version,
    ushort HardwareType,
    bool ExromHigh,
    bool GameHigh,
    byte Subtype,
    string Name);

public sealed record C64CrtChip(
    C64CrtChipType Type,
    ushort Bank,
    ushort LoadAddress,
    byte[] Data);

public sealed record C64CrtImage(
    C64CrtHeader Header,
    IReadOnlyList<C64CrtChip> Chips);
