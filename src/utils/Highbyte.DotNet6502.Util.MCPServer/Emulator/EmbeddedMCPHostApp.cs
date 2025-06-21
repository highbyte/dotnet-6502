using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Util.MCPServer.Emulator;

/// <summary>
/// Host app for running Highbyte.DotNet6502 emulator embbedded in a MCP server
/// </summary>
public class EmbeddedMCPHostApp : HostApp<NullRenderContext, NullInputHandlerContext, NullAudioHandlerContext>
{
    // --------------------
    // Injected variables
    // --------------------
    private readonly ILogger _logger;

    private readonly DotNet6502InMemLogStore _logStore;
    private readonly DotNet6502InMemLoggerConfiguration _logConfig;
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;

    // --------------------
    // Other variables / constants
    // --------------------
    private NullRenderContext _renderContext = default!;
    private NullInputHandlerContext _inputHandlerContext = default!;
    private NullAudioHandlerContext _audioHandlerContext = default!;


    private const int STATS_UPDATE_EVERY_X_FRAME = 60 * 1;
    private const int DEBUGINFO_UPDATE_EVERY_X_FRAME = 10 * 1;

    private int _statsFrameCount = 0;
    private int _debugInfoFrameCount = 0;

    private const int LOGS_UPDATE_EVERY_X_FRAME = 60 * 1;
    private int _logsFrameCount = 0;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="systemList"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="emulatorConfig"></param>
    /// <param name="logStore"></param>
    /// <param name="logConfig"></param>
    public EmbeddedMCPHostApp(
        SystemList<NullRenderContext, NullInputHandlerContext, NullAudioHandlerContext> systemList,
        ILoggerFactory loggerFactory,
        EmulatorConfig emulatorConfig,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig,
        IConfiguration configuration)
        : base("MCPServer", systemList, loggerFactory)
    {
        _logStore = logStore;
        _logConfig = logConfig;
        _configuration = configuration;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(typeof(EmbeddedMCPHostApp).Name);
    }

    public void Init()
    {
        _renderContext = new NullRenderContext();
        _inputHandlerContext = new NullInputHandlerContext();
        _audioHandlerContext = new NullAudioHandlerContext();

        SetContexts(() => _renderContext, () => _inputHandlerContext, () => _audioHandlerContext);
        InitRenderContext();
        InitInputHandlerContext();
        InitAudioHandlerContext();
    }

    public override void OnAfterSelectSystem()
    {
    }

    public override bool OnBeforeStart(ISystem systemAboutToBeStarted)
    {
        return true;
    }

    public override void OnAfterStart(EmulatorState emulatorStateBeforeStart)
    {
    }

    public override void OnBeforeStop()
    {
    }

    public override void OnAfterStop()
    {
    }

    public override void OnAfterClose()
    {
    }


    public override void OnBeforeRunEmulatorOneFrame(out bool shouldRun, out bool shouldReceiveInput)
    {
        shouldRun = true;
        shouldReceiveInput = false;
    }

    public override void OnAfterRunEmulatorOneFrame(ExecEvaluatorTriggerResult execEvaluatorTriggerResult)
    {
    }

    public override void OnBeforeDrawFrame(bool emulatorWillBeRendered)
    {
        // If any ImGui window is visible, make sure to clear Gl buffer before rendering emulator
        if (emulatorWillBeRendered)
        {
        }
    }
    public override void OnAfterDrawFrame(bool emulatorRendered)
    {
        if (emulatorRendered)
        {
        }
    }
}
