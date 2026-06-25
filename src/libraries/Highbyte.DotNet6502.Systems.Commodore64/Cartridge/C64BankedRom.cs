namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge;

/// <summary>
/// Validated fixed-size ROM banks used by bank-switched cartridges.
/// Missing banks read as unprogrammed ROM.
/// </summary>
public sealed class C64BankedRom
{
    public const int BankSize = 0x2000;

    private readonly IReadOnlyDictionary<ushort, byte[]> _banks;

    public C64BankedRom(
        IEnumerable<KeyValuePair<ushort, byte[]>> banks,
        ushort maximumBank)
    {
        ArgumentNullException.ThrowIfNull(banks);

        var copiedBanks = new Dictionary<ushort, byte[]>();
        foreach (var (bank, data) in banks)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (bank > maximumBank)
                throw new ArgumentOutOfRangeException(nameof(banks), $"ROM bank {bank} exceeds maximum bank {maximumBank}.");
            if (data.Length != BankSize)
                throw new ArgumentException($"ROM bank {bank} must be exactly {BankSize} bytes.", nameof(banks));
            if (!copiedBanks.TryAdd(bank, data.ToArray()))
                throw new ArgumentException($"ROM bank {bank} is duplicated.", nameof(banks));
        }

        if (copiedBanks.Count == 0)
            throw new ArgumentException("At least one ROM bank must be supplied.", nameof(banks));

        _banks = copiedBanks;
        HighestBank = copiedBanks.Keys.Max();
    }

    public int Count => _banks.Count;
    public ushort HighestBank { get; }

    public byte Read(ushort bank, ushort address)
        => _banks.TryGetValue(bank, out var data)
            ? data[address & (BankSize - 1)]
            : (byte)0xFF;
}
