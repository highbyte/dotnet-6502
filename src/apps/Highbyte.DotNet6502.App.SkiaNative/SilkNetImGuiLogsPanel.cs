using System.Numerics;
using Highbyte.DotNet6502.Logging;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.SkiaNative;

public class SilkNetImGuiLogsPanel : ISilkNetImGuiWindow
{
    private readonly DotNet6502InMemLogStore _logStore;
    private readonly DotNet6502InMemLoggerConfiguration _logConfig;

    public bool Visible { get; private set; }
    public bool WindowIsFocused { get; private set; }

    private const int POS_X = 300;
    private const int POS_Y = 2;
    private const int WIDTH = 950;
    private const int HEIGHT = 600;
    private static Vector4 s_labelColor = new Vector4(0.7f, 0.7f, 0.7f, 1.0f);
    private static Vector4 s_informationColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
    private static Vector4 s_errorColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    private static Vector4 s_warningColor = new Vector4(0.5f, 0.8f, 0.8f, 1);

    private readonly string[] _logLevelNames = Enum.GetNames<LogLevel>();
    private int _selectedLogLevel;
    private string _maxLogMessages = "";

    public SilkNetImGuiLogsPanel(DotNet6502InMemLogStore logStore, DotNet6502InMemLoggerConfiguration logConfig)
    {
        _logStore = logStore;
        _logConfig = logConfig;

        _selectedLogLevel = _logLevelNames.ToList().IndexOf(logConfig.LogLevel.ToString());
        _maxLogMessages = _logStore.MaxLogMessages.ToString();
    }

    public void PostOnRender()
    {
        ImGui.SetNextWindowSize(new Vector2(WIDTH, HEIGHT), ImGuiCond.Once);
        ImGui.SetNextWindowPos(new Vector2(POS_X, POS_Y), ImGuiCond.Once);

        //ImGui.SetWindowPos(new Vector2(POS_X, POS_Y));
        //ImGui.SetWindowSize(new Vector2(WIDTH, HEIGHT));

        ImGui.Begin($"Logs");

        // Clear Log
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
        if (ImGui.Button("Clear log"))
        {
            _logStore.Clear();
        }
        ImGui.PopStyleColor();

        // Set Log Level
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
        ImGui.Text("Log level:");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
        ImGui.PushItemWidth(120);
        if (ImGui.Combo("##logLevel", ref _selectedLogLevel, _logLevelNames, _logLevelNames.Length))
        {
            _logConfig.LogLevel = Enum.Parse<LogLevel>(_logLevelNames[_selectedLogLevel]);
        }
        ImGui.PopItemWidth();
        ImGui.PopStyleColor();

        // Set Max Message count
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
        ImGui.Text("Max messages:");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, s_informationColor);
        ImGui.PushItemWidth(40);
        //ImGui.SetKeyboardFocusHere(0);
        if (ImGui.InputText("", ref _maxLogMessages, 5, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.CharsNoBlank))
        {
            if (int.TryParse(_maxLogMessages, out int maxLogMessages))
                _logStore.MaxLogMessages = maxLogMessages;
        }
        ImGui.PopItemWidth();
        ImGui.PopStyleColor();

        // List log messages
        ImGui.PushStyleColor(ImGuiCol.Text, s_labelColor);
        foreach (var line in _logStore.GetLogMessages())
        {
            ImGui.Text(line);
        }
        ImGui.PopStyleColor();

        WindowIsFocused = ImGui.IsWindowFocused();

        ImGui.End();
    }

    public void Enable()
    {
        Visible = true;
    }

    public void Disable()
    {
        Visible = false;
    }
}
