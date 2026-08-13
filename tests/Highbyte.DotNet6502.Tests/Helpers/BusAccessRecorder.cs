namespace Highbyte.DotNet6502.Tests.Helpers;

/// <summary>
/// Records the ordered bus accesses (reads and writes) a CPU makes to watched address
/// ranges, for asserting instruction-atomic access SEQUENCES — the level-2 accuracy the
/// ordered-bus-accesses work is about.
///
/// Implemented purely with the production memory-mapped-I/O mechanism
/// (<see cref="Memory.MapReader"/>/<see cref="Memory.MapWriter"/>) — the same seam real
/// devices use — so NO tracing hooks exist on the production memory path. Watched
/// addresses behave as ordinary RAM (values seeded from the memory's current contents,
/// writes stored and read back), with every access appended to <see cref="Accesses"/>.
/// </summary>
public sealed class BusAccessRecorder
{
    public readonly record struct BusAccess(bool IsRead, ushort Address, byte Value)
    {
        public override string ToString() => $"{(IsRead ? "R" : "W")} {Address:x4}={Value:x2}";
    }

    private readonly List<BusAccess> _accesses = new();
    private readonly Dictionary<ushort, byte> _store = new();

    public IReadOnlyList<BusAccess> Accesses => _accesses;

    /// <summary>Watched-RAM contents (reflects writes made through the CPU).</summary>
    public byte this[ushort address]
    {
        get => _store[address];
        set => _store[address] = value;
    }

    /// <summary>
    /// Starts recording accesses to <paramref name="length"/> addresses from
    /// <paramref name="startAddress"/>. Current memory contents become the watched RAM's
    /// initial values.
    /// </summary>
    public void Watch(Memory mem, ushort startAddress, int length)
    {
        for (var i = 0; i < length; i++)
        {
            var address = (ushort)(startAddress + i);
            _store[address] = mem[address];
            mem.MapReader(address, a =>
            {
                var value = _store[a];
                _accesses.Add(new BusAccess(IsRead: true, a, value));
                return value;
            });
            mem.MapWriter(address, (a, value) =>
            {
                _accesses.Add(new BusAccess(IsRead: false, a, value));
                _store[a] = value;
            });
        }
    }

    /// <summary>Forget accesses recorded so far (watched RAM contents are kept).</summary>
    public void Clear() => _accesses.Clear();

    /// <summary>Compact "R f001=12 W f001=13" rendering for assertion failure messages.</summary>
    public string Describe() => string.Join(" ", _accesses);
}
