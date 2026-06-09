using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Rendering;

namespace Highbyte.DotNet6502.Monitor.Tests.Helpers;

/// <summary>
/// Minimal <see cref="ISystem"/> with a real <see cref="CPU"/> and <see cref="Memory"/> so the
/// monitor's disassembly ('d') and memory dump ('m') commands can be exercised. Members not used
/// by those commands are left unimplemented.
/// </summary>
internal sealed class TestSystem : ISystem
{
    public string Name => "Test";
    public List<string> SystemInfo => new();
    public List<KeyValuePair<string, Func<string>>> DebugInfo => new();

    public CPU CPU { get; } = new();
    public Memory Mem { get; } = new();
    public IScreen Screen => throw new NotImplementedException();

    public bool InstrumentationEnabled { get; set; }
    public Instrumentations Instrumentations { get; } = new();

    public IRenderProvider? RenderProvider => null;
    public List<IRenderProvider> RenderProviders { get; } = new();

    public ExecEvaluatorTriggerResult ExecuteOneFrame(IExecEvaluator? execEvaluator = null) => new();

    public ExecEvaluatorTriggerResult ExecuteOneInstruction(out InstructionExecResult instructionExecResult, IExecEvaluator? execEvaluator = null)
    {
        instructionExecResult = new InstructionExecResult();
        return new();
    }
}

/// <summary>
/// Concrete <see cref="MonitorBase"/> used in tests. Captures everything written to the monitor
/// output so command behavior can be asserted.
/// </summary>
internal sealed class TestMonitor : MonitorBase
{
    public List<string> Output { get; } = new();

    public TestMonitor(SystemRunner systemRunner, MonitorConfig config)
        : base(systemRunner, config)
    {
    }

    /// <summary>The first line written by the most recent command (or null if none).</summary>
    public string? FirstOutputLine => Output.Count > 0 ? Output[0] : null;

    public void ClearOutput() => Output.Clear();

    public override void WriteOutput(string message) => Output.Add(message);

    public override void WriteOutput(string message, MessageSeverity severity) => Output.Add(message);

    public override bool LoadBinary(string fileName, out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null, Action<MonitorBase, ushort, ushort>? afterLoadCallback = null)
        => throw new NotImplementedException();

    public override bool LoadBinary(out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null, Action<MonitorBase, ushort, ushort>? afterLoadCallback = null)
        => throw new NotImplementedException();

    public override void SaveBinary(string fileName, ushort startAddress, ushort endAddress, bool addFileHeaderWithLoadAddress)
        => throw new NotImplementedException();
}
