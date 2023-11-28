using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Tests.Systems;

public class SystemRunnerBuilderTests
{
    [Fact]
    public void WithRenderer_WhenGivenARenderer_SetsTheRendererOnTheSystemRunner()
    {
        // Arrange
        var system = new TestSystem();
        var renderer = new TestRenderer();
        var builder = new SystemRunnerBuilder<TestSystem, IRenderContext, IInputHandlerContext, IAudioHandlerContext>(system);

        // Act
        builder.WithRenderer(renderer);

        // Assert
        var result = builder.Build();
        Assert.Equal(renderer, result.Renderer);
    }

    [Fact]
    public void WithRenderer_WhenGivenANonGenericRenderer_SetsTheRendererOnTheSystemRunner()
    {
        // Arrange
        var system = new TestSystem();
        var renderer = new TestRendererNonGeneric();
        var builder = new SystemRunnerBuilder<TestSystem, IRenderContext, IInputHandlerContext, IAudioHandlerContext>(system);

        // Act
        builder.WithRenderer(renderer);

        // Assert
        var result = builder.Build();
        Assert.Equal(renderer, result.Renderer);
    }

    [Fact]
    public void WithInputHandler_WhenGivenAnInputHandler_SetsTheInputHandlerOnTheSystemRunner()
    {
        // Arrange
        var system = new TestSystem();
        var handler = new TestInputHandler();
        var builder = new SystemRunnerBuilder<TestSystem, IRenderContext, IInputHandlerContext, IAudioHandlerContext>(system);

        // Act
        builder.WithInputHandler(handler);

        // Assert
        var result = builder.Build();
        Assert.Equal(handler, result.InputHandler);
    }
    [Fact]
    public void WithInputHandler_WhenGivenAnNonGenreicInputHandler_SetsTheInputHandlerOnTheSystemRunner()
    {
        // Arrange
        var system = new TestSystem();
        var handler = new TestInputHandlerNonGeneric();
        var builder = new SystemRunnerBuilder<TestSystem, IRenderContext, IInputHandlerContext, IAudioHandlerContext>(system);

        // Act
        builder.WithInputHandler(handler);

        // Assert
        var result = builder.Build();
        Assert.Equal(handler, result.InputHandler);
    }

    [Fact]
    public void WithAudioHandler_WhenGivenAnAudioHandler_SetsTheAudioHandlerOnTheSystemRunner()
    {
        // Arrange
        var system = new TestSystem();
        var handler = new TestAudioHandler();
        var builder = new SystemRunnerBuilder<TestSystem, IRenderContext, IInputHandlerContext, IAudioHandlerContext>(system);

        // Act
        builder.WithAudioHandler(handler);

        // Assert
        var result = builder.Build();
        Assert.Equal(handler, result.AudioHandler);
    }

    [Fact]
    public void WithAudioHandler_WhenGivenAnNonGenericAudioHandler_SetsTheAudioHandlerOnTheSystemRunner()
    {
        // Arrange
        var system = new TestSystem();
        var handler = new TestAudioHandlerNonGeneric();
        var builder = new SystemRunnerBuilder<TestSystem, IRenderContext, IInputHandlerContext, IAudioHandlerContext>(system);

        // Act
        builder.WithAudioHandler(handler);

        // Assert
        var result = builder.Build();
        Assert.Equal(handler, result.AudioHandler);
    }
}

public class TestSystem : ISystem
{
    public string Name => "Test";

    public List<string> SystemInfo => new List<string>();

    public CPU CPU => throw new NotImplementedException();

    public Memory Mem => throw new NotImplementedException();

    public IScreen Screen => throw new NotImplementedException();

    public Instrumentations Stats { get; } = new();

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

public class TestRenderer : IRenderer<TestSystem, IRenderContext>
{
    public void Draw(TestSystem system)
    {
    }
    public void Draw(ISystem system)
    {
    }
    public void Init(TestSystem system, IRenderContext renderContext)
    {
    }
    public void Init(ISystem system, IRenderContext renderContext)
    {
    }
    public Instrumentations Stats { get; } = new();
}
public class TestRendererNonGeneric : IRenderer
{
    public void Draw(ISystem system)
    {
    }
    public void Init(ISystem system, IRenderContext renderContext)
    {
    }
    public Instrumentations Stats { get; } = new();
}

public class TestInputHandler : IInputHandler<TestSystem, IInputHandlerContext>
{
    public void Init(TestSystem system, IInputHandlerContext inputContext)
    {
    }
    public void Init(ISystem system, IInputHandlerContext inputContext)
    {
    }
    public void ProcessInput(TestSystem system)
    {
    }
    public void ProcessInput(ISystem system)
    {
    }

    public List<string> GetStats() => new();

    public Instrumentations Stats { get; } = new();
}

public class TestInputHandlerNonGeneric : IInputHandler
{
    public List<string> GetStats()
    {
        return new List<string>();
    }
    public void Init(ISystem system, IInputHandlerContext inputContext)
    {
    }
    public void ProcessInput(ISystem system)
    {
    }
    public Instrumentations Stats { get; } = new();
}

public class TestAudioHandler : IAudioHandler<TestSystem, IAudioHandlerContext>
{
    public void GenerateAudio(TestSystem system)
    {
    }
    public void GenerateAudio(ISystem system)
    {
    }
    public void Init(TestSystem system, IAudioHandlerContext audioHandlerContext)
    {
    }
    public void Init(ISystem system, IAudioHandlerContext audioHandlerContext)
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
    public List<string> GetStats() => new();

    public Instrumentations Stats { get; } = new();

}

public class TestAudioHandlerNonGeneric : IAudioHandler
{
    public void GenerateAudio(ISystem system)
    {
    }
    public void Init(ISystem system, IAudioHandlerContext audioHandlerContext)
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
    public List<string> GetStats() => new();

    public Instrumentations Stats { get; } = new();

}
