using System.Numerics;

namespace Highbyte.DotNet6502.App.SilkNetNative;

public class SilkNetImGuiDebugPanel : ISilkNetImGuiWindow
{
    public bool Visible { get; private set; }
    public bool WindowIsFocused { get; private set; }

    private const int POS_X = 600;
    private const int POS_Y = 300;
    private const int WIDTH = 500;
    private const int HEIGHT = 300;
    private static Vector4 s_labelColor = new Vector4(0.7f, 0.7f, 0.7f, 1.0f);

    private readonly Func<List<KeyValuePair<string, Func<string>>>> _getDebugInfo;

    public SilkNetImGuiDebugPanel(Func<List<KeyValuePair<string, Func<string>>>> getDebugInfo)
    {
        _getDebugInfo = getDebugInfo;
    }

    public void PostOnRender()
    {
        ImGui.SetNextWindowSize(new Vector2(WIDTH, HEIGHT), ImGuiCond.Once);
        ImGui.SetNextWindowPos(new Vector2(POS_X, POS_Y), ImGuiCond.Once);
        ImGui.SetNextWindowCollapsed(true, ImGuiCond.Appearing);

        //ImGui.SetWindowPos(new Vector2(POS_X, POS_Y));
        //ImGui.SetWindowSize(new Vector2(WIDTH, HEIGHT));

        var strings = new List<string>();

        if (_getDebugInfo != null)
        {
            foreach (var debugInfoItem in _getDebugInfo())
            {
                string line = debugInfoItem.Key + ": " + debugInfoItem.Value();
                strings.Add(line);
            };
        }

        ImGui.Begin($"DebugInfo");

        ImGui.PushStyleColor(ImGuiCol.Text, s_labelColor);
        foreach (var line in strings)
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
