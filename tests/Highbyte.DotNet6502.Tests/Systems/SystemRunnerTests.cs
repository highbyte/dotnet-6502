using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Tests.Systems;

public class SystemRunnerTests
{
    [Fact]
    public void CallingCleanUpWillCleanUpRenderer()
    {
        // Arrange
        var system = new TestSystem();
        var renderer = new TestRenderer(system, new NullRenderContext());
        var systemRunner = new SystemRunner(system, renderer);
        systemRunner.Init();

        // Act
        systemRunner.Cleanup();

        // Assert
        Assert.True(renderer.CleanUpWasCalled);
    }
}

public class TestSystem : ISystem
{
    public string Name => "Test";

    public List<string> SystemInfo => new List<string>();

    public CPU CPU => throw new NotImplementedException();

    public Memory Mem => throw new NotImplementedException();

    public IScreen Screen => throw new NotImplementedException();

    public bool InstrumentationEnabled { get; set; } = false;

    public Instrumentations Instrumentations { get; } = new();

    public ExecEvaluatorTriggerResult ExecuteOneFrame(SystemRunner systemRunner, IExecEvaluator? execEvaluator = null)
    {
        return new ExecEvaluatorTriggerResult();
    }

    public ExecEvaluatorTriggerResult ExecuteOneInstruction(SystemRunner systemRunner, out InstructionExecResult instructionExecResult, IExecEvaluator? execEvaluator = null)
    {
        instructionExecResult = new InstructionExecResult();
        return new ExecEvaluatorTriggerResult();
    }
}

public class TestRenderer : IRenderer
{
    private readonly TestSystem _system;
    public ISystem System => _system;

    private readonly IRenderContext _renderContext;
    public bool CleanUpWasCalled = false;

    public TestRenderer(TestSystem system, IRenderContext renderContext)
    {
        _system = system;
        _renderContext = renderContext;
    }
    public void Init()
    {
    }
    public void DrawFrame()
    {
    }
    public void Cleanup()
    {
        CleanUpWasCalled = true;
    }
    public Instrumentations Instrumentations { get; } = new();
}

public class TestInputHandler : IInputHandler
{
    private readonly TestSystem _system;
    public ISystem System => _system;

    private readonly IInputHandlerContext _inputContext;

    public TestInputHandler(TestSystem system, IInputHandlerContext inputContext)
    {
        _system = system;
        _inputContext = inputContext;
    }
    public void Init()
    {
    }
    public void BeforeFrame()
    {
    }
    public void Cleanup()
    {
        _inputContext.Cleanup();
    }
    public List<string> GetDebugInfo() => new();

    public Instrumentations Instrumentations { get; } = new();
}

public class TestAudioHandler : IAudioHandler
{
    private readonly TestSystem _system;
    public ISystem System => _system;

    private readonly IAudioHandlerContext _audioHandlerContext;
    public bool StopPlayingWasCalled = false;

    public TestAudioHandler(TestSystem system, IAudioHandlerContext audioHandlerContext)
    {
        _system = system;
        _audioHandlerContext = audioHandlerContext;
    }

    public void Init()
    {
    }
    public void AfterFrame()
    {
    }
    public void PausePlaying()
    {
    }
    public void StartPlaying()
    {
    }
    public void StopPlaying()
    {
        StopPlayingWasCalled = true;
    }
    public void Cleanup()
    {
        StopPlaying();
    }
    public List<string> GetDebugInfo() => new();

    public Instrumentations Instrumentations { get; } = new();
}
