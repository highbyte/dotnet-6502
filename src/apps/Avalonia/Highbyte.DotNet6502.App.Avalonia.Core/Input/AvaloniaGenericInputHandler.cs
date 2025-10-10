using System.Collections.Generic;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Input;

public class AvaloniaGenericInputHandler : IInputHandler
{
    private readonly GenericComputer _genericComputer;
    private readonly AvaloniaInputHandlerContext _inputHandlerContext;
    private readonly ILogger _logger;

    public Instrumentations Instrumentations { get; } = new();
    public ISystem System => _genericComputer;

    public AvaloniaGenericInputHandler(
        GenericComputer genericComputer, 
        AvaloniaInputHandlerContext inputHandlerContext, 
        ILoggerFactory loggerFactory)
    {
        _genericComputer = genericComputer;
        _inputHandlerContext = inputHandlerContext;
        _logger = loggerFactory.CreateLogger(typeof(AvaloniaGenericInputHandler).Name);
    }

    public void Init()
    {
        // Initialize input handling
    }

    public void BeforeFrame()
    {
        // Called before each frame
    }

    public void ProcessInput()
    {
        // TODO: Process keyboard input for Generic computer
    }

    public List<string> GetDebugInfo()
    {
        return new List<string> { "Avalonia Generic Input Handler Debug Info" };
    }

    public void Cleanup()
    {
        // Cleanup input handling
    }
}
