using System.Numerics;
using Highbyte.DotNet6502.App.SilkNetNative.Core;
using Highbyte.DotNet6502.App.SilkNetNative.Shell.Generic.ConfigUI;
using Highbyte.DotNet6502.Impl.SilkNet.Generic;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.SilkNetNative.Shell.Generic;

/// <summary>
/// Per-frame ImGui drawer for the Generic-computer section of the SilkNet host's main menu.
/// Encapsulates what used to live in <c>SilkNetImGuiMenu.DrawGenericComputerConfig</c>.
/// </summary>
public sealed class GenericSilkNetImGuiMenu : IImGuiMenuContributor
{
    private static readonly Vector4 s_errorColor = new(1.0f, 0.0f, 0.0f, 1.0f);

    private readonly SilkNetHostApp _host;

    private SilkNetImGuiGenericComputerConfig? _configUI;

    private EmulatorState EmulatorState => _host.EmulatorState;

    public GenericSilkNetImGuiMenu(SilkNetHostApp host)
    {
        _host = host;
    }

    public void OnSelected()
    {
        // Generic computer has no widget-bound state to refresh.
    }

    public void Draw()
    {
        ImGui.BeginDisabled(disabled: EmulatorState != EmulatorState.Uninitialized);

        _configUI ??= new SilkNetImGuiGenericComputerConfig(_host);
        if (ImGui.Button("GenericComputer config"))
        {
            _configUI.Init(
                (GenericComputerHostConfig)_host.CurrentHostSystemConfig.Clone(),
                _host.SelectedSystemConfigurationVariant);
            ImGui.OpenPopup("GenericComputer config");
        }
        ImGui.EndDisabled();
        _configUI.PostOnRender("GenericComputer config");

        if (!_host.IsSystemConfigValid().Result)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, s_errorColor);
            ImGui.TextWrapped("Config has errors. Press GenericComputer config button.");
            ImGui.PopStyleColor();
        }
    }
}
