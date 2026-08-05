namespace Highbyte.DotNet6502.Systems.Tests.Apple2.TestRom;

/// <summary>
/// A minimal absolute-origin 6502 emitter with label fix-ups, just enough to hand-write the
/// synthetic Apple II test ROM without pre-computing branch offsets by hand.
/// </summary>
internal sealed class RomAssembler
{
    private readonly ushort _origin;
    private readonly List<byte> _bytes = new();
    private readonly Dictionary<string, ushort> _labels = new();
    private readonly List<Fixup> _fixups = new();

    public RomAssembler(ushort origin) => _origin = origin;

    public ushort CurrentAddress => (ushort)(_origin + _bytes.Count);

    public void Label(string name) => _labels.Add(name, CurrentAddress);

    /// <summary>Emits opcode bytes and/or immediate/zero-page operands verbatim.</summary>
    public void Emit(params byte[] bytes) => _bytes.AddRange(bytes);

    /// <summary>Emits an opcode with a 16-bit operand resolved from a label.</summary>
    public void EmitAbsolute(byte opcode, string label, int addend = 0)
    {
        _bytes.Add(opcode);
        _fixups.Add(new Fixup(_bytes.Count, label, Relative: false, addend));
        _bytes.Add(0);
        _bytes.Add(0);
    }

    /// <summary>Emits a branch opcode with an 8-bit displacement resolved from a label.</summary>
    public void EmitBranch(byte opcode, string label)
    {
        _bytes.Add(opcode);
        _fixups.Add(new Fixup(_bytes.Count, label, Relative: true, Addend: 0));
        _bytes.Add(0);
    }

    public byte[] Assemble()
    {
        var code = _bytes.ToArray();
        foreach (var fixup in _fixups)
        {
            if (!_labels.TryGetValue(fixup.Label, out var target))
                throw new InvalidOperationException($"Undefined label '{fixup.Label}'.");

            var value = (ushort)(target + fixup.Addend);
            if (fixup.Relative)
            {
                var nextInstructionAddress = _origin + fixup.Offset + 1;
                var displacement = value - nextInstructionAddress;
                if (displacement is < -128 or > 127)
                    throw new InvalidOperationException($"Branch to '{fixup.Label}' out of range ({displacement}).");
                code[fixup.Offset] = (byte)(sbyte)displacement;
            }
            else
            {
                code[fixup.Offset] = (byte)(value & 0xFF);
                code[fixup.Offset + 1] = (byte)(value >> 8);
            }
        }
        return code;
    }

    private readonly record struct Fixup(int Offset, string Label, bool Relative, int Addend);
}
