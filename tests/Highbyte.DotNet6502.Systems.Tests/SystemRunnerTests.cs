using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Rendering;

namespace Highbyte.DotNet6502.Systems.Tests;

public class SystemRunnerTests
{
    [Fact]
    public void CreatingWithRendererAndInputHandlerAndAudioHandlerWorks()
    {
        // Arrange
        var system = new TestSystem();
        var inputHandler = new TestInputHandler(system, new NullInputHandlerContext());
        var audioHandler = new TestAudioHandler(system, new NullAudioHandlerContext());
        var systemRunner = new SystemRunner(system, inputHandler, audioHandler);

        // Act
        systemRunner.Init();

        // Assert
        Assert.Equal(system, systemRunner.System);
        //Assert.Equal(renderer, systemRunner.Renderer);
        Assert.Equal(inputHandler, systemRunner.InputHandler);
        Assert.Equal(audioHandler, systemRunner.AudioHandler);
    }

    [Fact]
    public void CreatingWithOnlyRendererWorksAndSetsOthersToNullImplementations()
    {
        // Arrange
        var system = new TestSystem();
        var systemRunner = new SystemRunner(system);

        // Act
        systemRunner.Init();

        // Assert
        Assert.Equal(system, systemRunner.System);
        //Assert.Equal(renderer, systemRunner.Renderer);
        Assert.Equal(typeof(NullInputHandler), systemRunner.InputHandler.GetType());
        Assert.Equal(typeof(NullAudioHandler), systemRunner.AudioHandler.GetType());
    }

    [Fact]
    public void CreatingWithOnlyInputHandlerWorksAndSetsOthersToNullImplementations()
    {
        // Arrange
        var system = new TestSystem();
        var inputHandler = new TestInputHandler(system, new NullInputHandlerContext());
        var systemRunner = new SystemRunner(system, inputHandler);

        // Act
        systemRunner.Init();

        // Assert
        Assert.Equal(system, systemRunner.System);
        //Assert.Equal(typeof(NullRenderer), systemRunner.Renderer.GetType());
        Assert.Equal(inputHandler, systemRunner.InputHandler);
        Assert.Equal(typeof(NullAudioHandler), systemRunner.AudioHandler.GetType());
    }

    [Fact]
    public void CreatingWithOnlyAudioHandlerWorksAndSetsOthersToNullImplementations()
    {
        // Arrange
        var system = new TestSystem();
        var audioHandler = new TestAudioHandler(system, new NullAudioHandlerContext());
        var systemRunner = new SystemRunner(system, audioHandler);

        // Act
        systemRunner.Init();

        // Assert
        Assert.Equal(system, systemRunner.System);
        //Assert.Equal(typeof(NullRenderer), systemRunner.Renderer.GetType());
        Assert.Equal(typeof(NullInputHandler), systemRunner.InputHandler.GetType());
        Assert.Equal(audioHandler, systemRunner.AudioHandler);
    }

    [Fact]
    public void CreatingWithOnlyRendererAndInputHandlersWorksAndSetsOthersToNullImplementations()
    {
        // Arrange
        var system = new TestSystem();
        var inputHandler = new TestInputHandler(system, new NullInputHandlerContext());
        var systemRunner = new SystemRunner(system, inputHandler);

        // Act
        systemRunner.Init();

        // Assert
        Assert.Equal(system, systemRunner.System);
        //Assert.Equal(renderer, systemRunner.Renderer);
        Assert.Equal(inputHandler, systemRunner.InputHandler);
        Assert.Equal(typeof(NullAudioHandler), systemRunner.AudioHandler.GetType());
    }

    [Fact]
    public void CreatingWithDifferentSystemInInputHandlerFails()
    {
        // Arrange
        var system = new TestSystem();
        var system2 = new TestSystem();
        var inputHandler = new TestInputHandler(system2, new NullInputHandlerContext());
        var audioHandler = new TestAudioHandler(system, new NullAudioHandlerContext());

        // Act / Assert
        var ex = Assert.Throws<DotNet6502Exception>(() => new SystemRunner(system, inputHandler, audioHandler));
        Assert.Contains("InputHandler must be for the same system as the SystemRunner", ex.Message);
    }

    [Fact]
    public void CreatingWithDifferentSystemInAudioHandlerFails()
    {
        // Arrange
        var system = new TestSystem();
        var system2 = new TestSystem();
        var inputHandler = new TestInputHandler(system, new NullInputHandlerContext());
        var audioHandler = new TestAudioHandler(system2, new NullAudioHandlerContext());

        // Act / Assert
        var ex = Assert.Throws<DotNet6502Exception>(() => new SystemRunner(system, inputHandler, audioHandler));
        Assert.Contains("AudioHandler must be for the same system as the SystemRunner", ex.Message);
    }

    [Fact]
    public void CallingCleanUpWillCleanUpRendererAndInputHandlerAndAudioHandler()
    {
        // Arrange
        var system = new TestSystem();
        var inputHandler = new TestInputHandler(system, new NullInputHandlerContext());
        var audioHandler = new TestAudioHandler(system, new NullAudioHandlerContext());
        var systemRunner = new SystemRunner(system, inputHandler, audioHandler);
        systemRunner.Init();

        // Act
        systemRunner.Cleanup();

        // Assert
        Assert.True(inputHandler.CleanUpWasCalled);
        Assert.True(audioHandler.CleanUpWasCalled);
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

    private IRenderProvider? _renderProvider;
    public IRenderProvider? RenderProvider => _renderProvider;
    public List<IRenderProvider> RenderProviders { get; } = new();
}

public class TestSystem2 : ISystem
{
    public const string SystemName = "Test2";
    public string Name => SystemName;

    public List<string> SystemInfo => new List<string>();
    public List<KeyValuePair<string, Func<string>>> DebugInfo => new();

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

    private IRenderProvider? _renderProvider;
    public IRenderProvider? RenderProvider => _renderProvider;
    public List<IRenderProvider> RenderProviders { get; } = new();
}


public class TestInputHandler : IInputHandler
{
    private readonly TestSystem _system;
    public ISystem System => _system;

    private readonly IInputHandlerContext _inputContext;
    public bool CleanUpWasCalled = false;


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
        CleanUpWasCalled = true;
    }
    public List<string> GetDebugInfo() => new();

    public Instrumentations Instrumentations { get; } = new();
}

public class TestAudioHandler : IAudioHandler
{
    private readonly TestSystem _system;
    public ISystem System => _system;

    private readonly IAudioHandlerContext _audioHandlerContext;
    public bool CleanUpWasCalled = false;

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
    }
    public void Cleanup()
    {
        CleanUpWasCalled = true;
        StopPlaying();
    }
    public List<string> GetDebugInfo() => new();

    public Instrumentations Instrumentations { get; } = new();
}
