using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Rendering;

namespace Highbyte.DotNet6502.Systems.Tests;

public class SystemRunnerTests
{
    [Fact]
    public void CreatingWithSystemWorks()
    {
        // Arrange
        var system = new TestSystem();

        // Act
        var systemRunner = new SystemRunner(system);

        // Assert
        Assert.Equal(system, systemRunner.System);
    }

    [Fact]
    public void ProcessInputBeforeFrameInvokesTheSystemsInputConsumer()
    {
        // Arrange
        var system = new TestSystem();
        var inputConsumer = new TestInputConsumer(system);
        system.InputConsumer = inputConsumer;
        var systemRunner = new SystemRunner(system);

        // Act
        systemRunner.ProcessInputBeforeFrame();

        // Assert
        Assert.True(inputConsumer.BeforeFrameWasCalled);
    }

    [Fact]
    public void CallingCleanUpWillCleanUpTheSystemsInputConsumer()
    {
        // Arrange
        var system = new TestSystem();
        var inputConsumer = new TestInputConsumer(system);
        system.InputConsumer = inputConsumer;
        var systemRunner = new SystemRunner(system);

        // Act
        systemRunner.Cleanup();

        // Assert
        Assert.True(inputConsumer.CleanUpWasCalled);
    }

    [Fact]
    public void ProcessInputAndCleanupAreNoOpsWhenSystemHasNoInputConsumer()
    {
        // Arrange
        var system = new TestSystem();
        var systemRunner = new SystemRunner(system);

        // Act / Assert (no exception)
        systemRunner.ProcessInputBeforeFrame();
        systemRunner.Cleanup();
    }
}

public class TestSystem : ISystem
{
    public const string SystemName = "Test";
    public string Name => SystemName;

    public List<string> SystemInfo => new List<string>();
    public List<KeyValuePair<string, Func<string>>> DebugInfo => new();


    public CPU CPU => throw new NotImplementedException();

    public Memory Mem => throw new NotImplementedException();

    public IScreen Screen => new ScreenInfo(100, 50, 123, 67, 60);

    public bool InstrumentationEnabled { get; set; } = false;

    public Instrumentations Instrumentations { get; } = new();

    public ExecEvaluatorTriggerResult ExecuteOneFrame(IExecEvaluator? execEvaluator = null)
    {
        return new ExecEvaluatorTriggerResult();
    }

    public ExecEvaluatorTriggerResult ExecuteOneInstruction(out InstructionExecResult instructionExecResult, IExecEvaluator? execEvaluator = null)
    {
        instructionExecResult = new InstructionExecResult();
        return new ExecEvaluatorTriggerResult();
    }

    public IRenderProvider? RenderProvider => null;
    public List<IRenderProvider> RenderProviders { get; } = new();

    public IInputConsumer? InputConsumer { get; set; }
}

public class TestSystem2 : ISystem
{
    public const string SystemName = "Test2";
    public string Name => SystemName;

    public List<string> SystemInfo => new List<string>();
    public List<KeyValuePair<string, Func<string>>> DebugInfo => new();

    public CPU CPU => throw new NotImplementedException();

    public Memory Mem => throw new NotImplementedException();

    public IScreen Screen => new ScreenInfo(200, 100, 246, 134, 60);

    public bool InstrumentationEnabled { get; set; } = false;

    public Instrumentations Instrumentations { get; } = new();

    public ExecEvaluatorTriggerResult ExecuteOneFrame(IExecEvaluator? execEvaluator = null)
    {
        return new ExecEvaluatorTriggerResult();
    }

    public ExecEvaluatorTriggerResult ExecuteOneInstruction(out InstructionExecResult instructionExecResult, IExecEvaluator? execEvaluator = null)
    {
        instructionExecResult = new InstructionExecResult();
        return new ExecEvaluatorTriggerResult();
    }

    public IRenderProvider? RenderProvider => null;
    public List<IRenderProvider> RenderProviders { get; } = new();

    public IInputConsumer? InputConsumer { get; set; }
}


public class TestInputConsumer : IInputConsumer
{
    private readonly TestSystem _system;
    public ISystem System => _system;

    public bool InitWasCalled = false;
    public bool BeforeFrameWasCalled = false;
    public bool CleanUpWasCalled = false;

    public TestInputConsumer(TestSystem system)
    {
        _system = system;
    }
    public void Init(IHostInputState hostInputState)
    {
        InitWasCalled = true;
    }
    public void BeforeFrame()
    {
        BeforeFrameWasCalled = true;
    }
    public void Cleanup()
    {
        CleanUpWasCalled = true;
    }
    public List<string> GetDebugInfo() => new();

    public Instrumentations Instrumentations { get; } = new();
}
