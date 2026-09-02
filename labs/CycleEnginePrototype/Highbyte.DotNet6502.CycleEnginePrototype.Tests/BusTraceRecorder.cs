namespace Highbyte.DotNet6502.CycleEnginePrototype.Tests;

/// <summary>
/// Records the ordered bus accesses to watched addresses through the production memory-mapped
/// I/O seam, the same way the main test suite's recorder does. Watched addresses behave as RAM.
/// </summary>
internal sealed class BusTraceRecorder
{
    public readonly record struct BusAccess(bool IsRead, ushort Address, byte Value)
    {
        public override string ToString() => $"{(IsRead ? "R" : "W")} {Address:x4}={Value:x2}";
    }

    private readonly List<BusAccess> _accesses = new();
    private readonly Dictionary<ushort, byte> _store = new();

    public IReadOnlyList<BusAccess> Accesses => _accesses;

    public void Watch(Memory mem, ushort startAddress, int length)
    {
        for (var i = 0; i < length; i++)
        {
            var address = (ushort)(startAddress + i);
            _store[address] = mem[address];
            mem.MapReader(address, a =>
            {
                var value = _store[a];
                _accesses.Add(new BusAccess(true, a, value));
                return value;
            });
            mem.MapWriter(address, (a, value) =>
            {
                _accesses.Add(new BusAccess(false, a, value));
                _store[a] = value;
            });
        }
    }

    public string Describe() => string.Join(" ", _accesses);
}
