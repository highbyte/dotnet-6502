using System.Numerics;
using Highbyte.DotNet6502.App.SkiaNative.Instrumentation.Stats;
using Highbyte.DotNet6502.App.SkiaNative.Stats;
using Highbyte.DotNet6502.Systems;
using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;

namespace Highbyte.DotNet6502.App.SkiaNative
{
    public class SilkNetImGuiStatsPanel
    {
        public bool Visible = false;

        private readonly SystemRunner _systemRunner;

        private bool _hasBeenInitializedOnce = false;
        private const int POS_X = 2;
        private const int POS_Y = 2;
        private const int WIDTH = 80;
        private const int HEIGHT = 15;
        static Vector4 s_LabelColor = new Vector4(0.7f, 0.7f, 0.7f, 1.0f);

        public SilkNetImGuiStatsPanel(
            SystemRunner systemRunner)
        {
            _systemRunner = systemRunner;
        }

        public void PostOnRender(ImGuiController imGuiController, double deltaTime)
        {
            // Make sure ImGui is up-to-date
            imGuiController.Update((float)deltaTime);

            ImGui.SetWindowPos(new Vector2(POS_X, POS_Y));
            ImGui.SetWindowSize(new Vector2(WIDTH, HEIGHT));

            if (!_hasBeenInitializedOnce)
            {
                _hasBeenInitializedOnce = true;
            }

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
            ImGui.End();

            imGuiController?.Render();
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
}