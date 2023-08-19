using System.Numerics;
using Highbyte.DotNet6502.App.SkiaNative.Instrumentation.Stats;
using Highbyte.DotNet6502.App.SkiaNative.Stats;

namespace Highbyte.DotNet6502.App.SkiaNative;

public class SilkNetImGuiStatsPanel : ISilkNetImGuiWindow
{
    public bool Visible { get; private set; }
    public bool WindowIsFocused { get; private set; }

    private const int POS_X = 600;
    private const int POS_Y = 2;
    private const int WIDTH = 400;
    private const int HEIGHT = 300;
    static Vector4 s_LabelColor = new Vector4(0.7f, 0.7f, 0.7f, 1.0f);

    public SilkNetImGuiStatsPanel()
    {
    }

    public void PostOnRender()
    {
        ImGui.SetNextWindowSize(new Vector2(WIDTH, HEIGHT), ImGuiCond.Once);
        ImGui.SetNextWindowPos(new Vector2(POS_X, POS_Y), ImGuiCond.Once);

        //ImGui.SetWindowPos(new Vector2(POS_X, POS_Y));
        //ImGui.SetWindowSize(new Vector2(WIDTH, HEIGHT));

        var strings = new List<string>();
        foreach ((string name, IStat stat) in InstrumentationBag.Stats.OrderBy(i => i.Name))
        {
            if (stat.ShouldShow())
            {
                string line = name + ": " + stat.GetDescription();
                strings.Add(line);
            }
        };

        ImGui.Begin($"Stats");
        ImGui.PushStyleColor(ImGuiCol.Text, s_LabelColor);
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
