using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Tests.SingleStepTests;

/// <summary>
/// Runs a pinned subset of the SingleStepTests 65x02 corpus (MIT; see
/// <c>Fixtures/SingleStepTests/manifest.json</c> and <c>tools/singlesteptests/extract.py</c>).
/// Every vector is one instruction with the full CPU and memory state before and after and the
/// exact bus cycles the silicon performs: address, value and direction, one per clock cycle.
///
/// This is the oracle for "every cycle is a bus access at the right address": for each vector the
/// final registers, flags and memory must match, the recorded bus trace must match the corpus
/// cycle by cycle, and the instruction's reported cycle count must equal both the trace length and
/// the advance of <see cref="CPU.BusCycles"/>.
///
/// The <c>6502</c> set is run against the NMOS 6502 model with the full undocumented profile; the
/// <c>wdc65c02</c> set against the NCR 65C02 model. Bytes where the emulated part is documented to
/// differ from the corpus's part are listed in <see cref="KnownDeviations"/> with the reason, and
/// skipped rather than asserted.
/// </summary>
public class SingleStepVectorTests : IClassFixture<SingleStepVectorTests.Harness>
{
    private readonly Harness _harness;
    private readonly ITestOutputHelper _output;

    public SingleStepVectorTests(Harness harness, ITestOutputHelper output)
    {
        _harness = harness;
        _output = output;
    }

    private const string UnstableNmos =
        "unstable undocumented opcode (result depends on a chip-specific 'magic' value and bus timing); not implemented in any profile, the corpus encodes one silicon's behavior";
    private const string RockwellBitOps =
        "Rockwell/WDC bit instruction (RMB/SMB/BBR/BBS); the emulated NCR 65C02 executes these bytes as 1-cycle NOPs";

    /// <summary>Opcodes per set that are deliberately not asserted, with the reason.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<byte, string>> KnownDeviations =
        new Dictionary<string, IReadOnlyDictionary<byte, string>>
        {
            ["6502"] = new Dictionary<byte, string>
            {
                [0x8B] = UnstableNmos,  // ANE
                [0x93] = UnstableNmos,  // SHA (zp),Y
                [0x9B] = UnstableNmos,  // TAS
                [0x9C] = UnstableNmos,  // SHY
                [0x9E] = UnstableNmos,  // SHX
                [0x9F] = UnstableNmos,  // SHA abs,Y
                [0xAB] = UnstableNmos,  // LXA
            },
            ["wdc65c02"] = BuildWdcDeviations(),
        };

    /// <summary>
    /// Individual vectors that are deliberately not asserted: the 65C02 decimal-mode extra cycle
    /// of immediate ADC/SBC reads a fixed address in the corpus ($007F for ADC, $0000 for SBC)
    /// that no documentation explains; the emulator re-reads the operand byte instead.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<byte, (string Reason, Func<VectorState, bool> Applies)>> KnownVectorDeviations =
        new Dictionary<string, IReadOnlyDictionary<byte, (string, Func<VectorState, bool>)>>
        {
            ["6502"] = new Dictionary<byte, (string, Func<VectorState, bool>)>(),
            ["wdc65c02"] = new Dictionary<byte, (string, Func<VectorState, bool>)>
            {
                [0x69] = ("decimal-mode extra cycle of ADC # reads $007F in the corpus", state => (state.P & 0x08) != 0),
                [0xE9] = ("decimal-mode extra cycle of SBC # reads $0000 in the corpus", state => (state.P & 0x08) != 0),
            },
        };

    private static Dictionary<byte, string> BuildWdcDeviations()
    {
        var deviations = new Dictionary<byte, string>();
        for (var code = 0x07; code <= 0xFF; code += 0x08)
            deviations[(byte)code] = RockwellBitOps;         // $x7 and $xF columns
        deviations[0xCB] = "WAI is WDC-specific; the emulated NCR 65C02 executes the byte as a 1-cycle NOP";
        deviations[0xDB] = "STP is WDC-specific; the emulated NCR 65C02 executes the byte as a 1-cycle NOP";
        deviations[0x5C] = "the corpus runs the $5C NOP in 4 cycles; the emulated part uses the documented 8";
        return deviations;
    }

    public static IEnumerable<object[]> Nmos6502Opcodes()
        => Enumerable.Range(0, 256).Select(o => new object[] { (byte)o });

    public static IEnumerable<object[]> Wdc65c02Opcodes()
        => Enumerable.Range(0, 256).Select(o => new object[] { (byte)o });

    [Theory, MemberData(nameof(Nmos6502Opcodes))]
    public void Nmos6502(byte opcode)
        => RunOpcode("6502", opcode, () => new CPU(NullLoggerFactory.Instance, CpuModelIds.Nmos6502, CpuCompatibilityProfile.FullUnofficial));

    [Theory, MemberData(nameof(Wdc65c02Opcodes))]
    public void Wdc65c02(byte opcode)
        => RunOpcode("wdc65c02", opcode, () => new CPU(NullLoggerFactory.Instance, CpuModelIds.Ncr65c02, CpuCompatibilityProfile.OfficialOnly));

    private void RunOpcode(string set, byte opcode, Func<CPU> createCpu)
    {
        if (KnownDeviations[set].TryGetValue(opcode, out var reason))
        {
            _output.WriteLine($"{set} ${opcode:x2}: skipped — {reason}");
            return;
        }

        var vectors = _harness.Vectors(set, opcode);
        Assert.True(vectors.Count > 0, $"No vectors for {set} ${opcode:x2}; regenerate the fixtures with tools/singlesteptests/extract.py.");

        var cpu = createCpu();
        var descriptor = cpu.Descriptors[opcode];
        if (descriptor is not null && descriptor.Mnemonic == "JAM")
        {
            _output.WriteLine($"{set} ${opcode:x2}: JAM halts the emulated CPU; the corpus models the hardware's endless reads. Not compared.");
            return;
        }

        var failures = new List<string>();
        var skipped = 0;
        KnownVectorDeviations[set].TryGetValue(opcode, out var vectorDeviation);
        foreach (var vector in vectors)
        {
            if (vectorDeviation.Applies is not null && vectorDeviation.Applies(vector.Initial))
            {
                skipped++;
                continue;
            }
            var failure = _harness.Run(cpu, vector);
            if (failure is not null)
                failures.Add(failure);
        }
        if (skipped > 0)
            _output.WriteLine($"{set} ${opcode:x2}: {skipped} vectors skipped — {vectorDeviation.Reason}");
        Assert.True(vectors.Count - skipped > 0, $"{set} ${opcode:x2}: every vector was skipped");

        Assert.True(failures.Count == 0,
            $"{set} ${opcode:x2} ({descriptor?.Mnemonic ?? "undefined"} {descriptor?.Addressing}): {failures.Count} of {vectors.Count} vectors differ.\n" +
            string.Join("\n", failures.Take(3)));
    }

    // ----- harness -----

    public sealed record Vector(string Name, VectorState Initial, VectorState Final, List<int[]> CycleAddresses, List<int[]> CycleValues, List<string> CycleKinds);

    public sealed record VectorState(int Pc, int S, int A, int X, int Y, int P, List<int[]> Ram);

    /// <summary>
    /// One 64 KB tracing memory and the loaded fixture sets, shared by all cases in the class. The
    /// memory maps every address through the production memory-mapped-I/O seam so each access is
    /// recorded exactly as a real device would see it.
    /// </summary>
    public sealed class Harness
    {
        private readonly Dictionary<string, Dictionary<byte, List<Vector>>> _sets = new();
        private readonly byte[] _store = new byte[0x10000];
        private readonly List<(bool IsRead, ushort Address, byte Value)> _accesses = new();
        private readonly Memory _mem = new();

        public Harness()
        {
            for (var i = 0; i < 0x10000; i++)
            {
                var address = (ushort)i;
                _mem.MapReader(address, a =>
                {
                    var value = _store[a];
                    _accesses.Add((true, a, value));
                    return value;
                });
                _mem.MapWriter(address, (a, value) =>
                {
                    _accesses.Add((false, a, value));
                    _store[a] = value;
                });
            }
        }

        public IReadOnlyList<Vector> Vectors(string set, byte opcode)
        {
            if (!_sets.TryGetValue(set, out var byOpcode))
            {
                byOpcode = Load(set);
                _sets[set] = byOpcode;
            }
            return byOpcode.TryGetValue(opcode, out var list) ? list : [];
        }

        private static Dictionary<byte, List<Vector>> Load(string set)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SingleStepTests", $"{set}.jsonl.gz");
            Assert.True(File.Exists(path), $"Fixture {path} is missing; run tools/singlesteptests/extract.py.");
            var result = new Dictionary<byte, List<Vector>>();
            using var stream = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (reader.ReadLine() is { } line)
            {
                if (line.Length == 0)
                    continue;
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var vector = new Vector(
                    root.GetProperty("name").GetString()!,
                    ReadState(root.GetProperty("initial")),
                    ReadState(root.GetProperty("final")),
                    root.GetProperty("cycles").EnumerateArray().Select(c => new[] { c[0].GetInt32() }).ToList(),
                    root.GetProperty("cycles").EnumerateArray().Select(c => new[] { c[1].GetInt32() }).ToList(),
                    root.GetProperty("cycles").EnumerateArray().Select(c => c[2].GetString()!).ToList());
                var opcode = Convert.ToByte(vector.Name[..2], 16);
                if (!result.TryGetValue(opcode, out var list))
                    result[opcode] = list = new List<Vector>();
                list.Add(vector);
            }
            return result;
        }

        private static VectorState ReadState(JsonElement e)
            => new(
                e.GetProperty("pc").GetInt32(), e.GetProperty("s").GetInt32(), e.GetProperty("a").GetInt32(),
                e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("p").GetInt32(),
                e.GetProperty("ram").EnumerateArray().Select(r => new[] { r[0].GetInt32(), r[1].GetInt32() }).ToList());

        /// <summary>Runs one vector; returns null on success or a description of the first difference.</summary>
        public string? Run(CPU cpu, Vector vector)
        {
            Array.Clear(_store);
            _accesses.Clear();
            foreach (var cell in vector.Initial.Ram)
                _store[cell[0]] = (byte)cell[1];

            cpu.PC = (ushort)vector.Initial.Pc;
            cpu.SP = (byte)vector.Initial.S;
            cpu.A = (byte)vector.Initial.A;
            cpu.X = (byte)vector.Initial.X;
            cpu.Y = (byte)vector.Initial.Y;
            cpu.ProcessorStatus.Value = (byte)vector.Initial.P;
            var busCyclesBefore = cpu.BusCycles;

            var result = cpu.ExecuteOneInstructionMinimal(_mem);

            var expected = vector.Final;
            var diffs = new List<string>();
            void Check(string what, int exp, int act) { if (exp != act) diffs.Add($"{what} expected {exp:x} got {act:x}"); }
            Check("PC", expected.Pc, cpu.PC);
            Check("S", expected.S, cpu.SP);
            Check("A", expected.A, cpu.A);
            Check("X", expected.X, cpu.X);
            Check("Y", expected.Y, cpu.Y);
            // Bits 4 (B) and 5 have no register storage on the silicon; compare the six real flags.
            Check("P", expected.P & 0xCF, cpu.ProcessorStatus.Value & 0xCF);
            foreach (var cell in expected.Ram)
                if (_store[cell[0]] != (byte)cell[1])
                    diffs.Add($"RAM[{cell[0]:x4}] expected {cell[1]:x2} got {_store[cell[0]]:x2}");

            var expectedTrace = string.Join(" ", Enumerable.Range(0, vector.CycleKinds.Count)
                .Select(i => $"{(vector.CycleKinds[i] == "read" ? "R" : "W")} {vector.CycleAddresses[i][0]:x4}={vector.CycleValues[i][0]:x2}"));
            var actualTrace = string.Join(" ", _accesses.Select(a => $"{(a.IsRead ? "R" : "W")} {a.Address:x4}={a.Value:x2}"));
            if (expectedTrace != actualTrace)
                diffs.Add($"bus: expected [{expectedTrace}] got [{actualTrace}]");
            if ((ulong)vector.CycleKinds.Count != result.CyclesConsumed)
                diffs.Add($"cycles: corpus {vector.CycleKinds.Count}, reported {result.CyclesConsumed}");
            if (cpu.BusCycles - busCyclesBefore != result.CyclesConsumed)
                diffs.Add($"BusCycles advanced {cpu.BusCycles - busCyclesBefore}, reported {result.CyclesConsumed}");

            return diffs.Count == 0 ? null : $"  {vector.Name}: {string.Join("; ", diffs)}";
        }
    }
}
