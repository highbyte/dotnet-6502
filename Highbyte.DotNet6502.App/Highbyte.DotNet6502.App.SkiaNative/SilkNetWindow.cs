using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Systems;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using SkiaSharp;
using System.Diagnostics;
using System.Numerics;

namespace Highbyte.DotNet6502.App.SkiaNative;
public class SilkNetWindow<TSystem> 
    where TSystem: ISystem
{
    private static ImGuiController s_ImGuiController;
    private static GL s_Gl;


    private static IWindow s_window;
    private readonly Func<SkiaRenderContext, SilkNetInputHandlerContext, SystemRunner> _getSystemRunner;
    private readonly float _canvasScale;

    // SkipSharp context/surface/canvas
    private SkiaRenderContext _skiaRenderContext;
    // SilkNet input handling
    private SilkNetInputHandlerContext _silkNetInputHandlerContext;

    // Emulator    
    private SystemRunner _systemRunner;

    private bool _monitorEnabled = false;

    public SilkNetWindow(
        IWindow window,
        Func<SkiaRenderContext, SilkNetInputHandlerContext, SystemRunner> getSystemRunner,
        float scale = 1.0f) 
    {
        s_window = window;
        _getSystemRunner = getSystemRunner;
        _canvasScale = scale;
    }

    public void Run()
    {
        s_window.Load += OnLoad;
        s_window.Closing += OnClosing;
        s_window.Update += OnUpdate;
        s_window.Render += OnRender;
        s_window.Resize += OnResize;

        s_window.Run();
    }

    protected void OnLoad()
    {
        // Init SkipSharp resources (must be done in OnLoad, otherwise no OpenGL context will exist create by SilkNet.)
        _skiaRenderContext = new SkiaRenderContext(s_window.Size.X, s_window.Size.Y, _canvasScale);
        _silkNetInputHandlerContext = new SilkNetInputHandlerContext(s_window);
        _systemRunner = _getSystemRunner(_skiaRenderContext, _silkNetInputHandlerContext);

        // Init ImgUI resources 
        InitMonitorUI();
    }

    protected void OnClosing()
    {
        // Cleanup Skia resources
        _skiaRenderContext.Cleanup();

        // Cleanup SilkNet input resources
        _silkNetInputHandlerContext.Cleanup();

        // Dispose ImgUI
        s_ImGuiController?.Dispose();

        // Cleanup SilNet window resources
        s_window?.Dispose();

    }

    /// <summary>
    /// Runs on every Update Frame event.
    /// 
    /// Use this method to run logic.
    /// 
    /// </summary>
    /// <param name=""></param>
    protected void OnUpdate(double deltaTime)
    {
        if(_monitorEnabled)
        {
            return;
        }

        if(_silkNetInputHandlerContext.Exit)
        {
            s_window.Close();
            return;
        }

        // Run emulator.
        // Handle input
        _systemRunner.ProcessInput();

        // Run emulator for one frame worth of emulated CPU cycles 
        _systemRunner.RunEmulatorOneFrame();
    }


    /// <summary>
    /// Runs on every Render Frame event.
    /// 
    /// Use this method to render the world.
    /// 
    /// This method is called at a RenderFrequency set in the GameWindowSettings object.
    /// </summary>
    /// <param name="args"></param>
    protected void OnRender(double deltaTime)
    {
        if(_monitorEnabled)
        {
            s_Gl.Clear((uint) (ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
        }
        
        // Render emulator system screen
        _systemRunner.Draw();

        // Flush the Skia Context
        _skiaRenderContext.GRContext?.Flush();

        // SilkNet windows are what's known as "double-buffered". In essence, the window manages two buffers.
        // One is rendered to while the other is currently displayed by the window.
        // This avoids screen tearing, a visual artifact that can happen if the buffer is modified while being displayed.
        // After drawing, call this function to swap the buffers. If you don't, it won't display what you've rendered.

        // NOTE: s_window.SwapBuffers() seem to have some problem. Window is darker, and some flickering.
        //       Use windowOptions.ShouldSwapAutomatically = true  instead
        //s_window.SwapBuffers();

        // Render monitor if enabled
        if(_monitorEnabled)
        {
            DrawMonitorUI(deltaTime);
        }

    }

    private void OnResize(Vector2D<int> vec2)
    {
    }
    

    private void InitMonitorUI()
    {
        s_Gl = GL.GetApi(s_window);

        s_ImGuiController = new ImGuiController(
            s_Gl,
            s_window, // pass in our window
            _silkNetInputHandlerContext.InputContext // input context
        );
        ImGuiNET.ImGui.SetWindowPos(new Vector2(10, 10));

        // Init monitor list of history commands with blanks
        for (int i = 0; i < MONITOR_CMD_HISTORY_VIEW_ROWS; i++)
            _monitorCmdHistory.Add("");

        // Listen to key to enable monitor
        _silkNetInputHandlerContext.PrimaryKeyboard.KeyDown += OnMonitorKeyDown;
    }
    private void OnMonitorKeyDown(IKeyboard keyboard, Key key, int x)
    {
        if(key == Key.F12)
            _monitorEnabled = !_monitorEnabled;
    }

    private void DrawMonitorUI(double deltaTime)
    {
        // Make sure ImGui is up-to-date
        s_ImGuiController.Update((float)deltaTime);

        ImGui.Begin("Monitor");
        ImGui.SetWindowSize(new Vector2(620, 400));
        //ImGuiNET.ImGui.Text("Commands");

        foreach (var cmd in _monitorCmdHistory)
        {
            ImGui.Text(cmd);
        }

        ImGui.SetKeyboardFocusHere(0);
        ImGui.PushItemWidth(600);
        if (ImGui.InputText("", ref _monitorCmdString, MONITOR_CMD_LINE_LENGTH, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            _monitorCmdHistory.Add(_monitorCmdString);
            _monitorCmdString = "";
            if (_monitorCmdHistory.Count > MONITOR_CMD_HISTORY_VIEW_ROWS)
                _monitorCmdHistory.RemoveAt(0);
        }

        s_ImGuiController.Render();
    }

    int MONITOR_CMD_HISTORY_VIEW_ROWS = 20;
    const int MONITOR_CMD_LINE_LENGTH = 80;
    List<string> _monitorCmdHistory = new();

    string _monitorCmdString = "";
}