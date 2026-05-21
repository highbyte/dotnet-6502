namespace Highbyte.DotNet6502.App.SilkNetNative.Core;

/// <summary>
/// SilkNet/ImGui host-side contract for per-system menu content.
/// A shell plug-in (<c>App.SilkNetNative.Shell.&lt;System&gt;</c>) returns one of these from
/// <c>ISystemShellPlugin.CreateMenuContribution</c>. The host's main menu invokes
/// <see cref="Draw"/> each frame when this system is selected.
/// </summary>
/// <remarks>
/// This is the ImGui analogue of an Avalonia ViewModel + View pair. Because ImGui is
/// immediate-mode, there is no data-binding — the plug-in owns its widget state and
/// draws procedurally. The host only forwards the frame call.
/// </remarks>
public interface IImGuiMenuContributor
{
    /// <summary>
    /// Called when this contributor becomes active — i.e. on startup if its system is the
    /// default, when the user picks this system in the dropdown, or when the configuration
    /// variant of an already-selected system changes. Use it to refresh widget-bound state
    /// from the current host config.
    /// </summary>
    void OnSelected();

    /// <summary>
    /// Draw the system-specific section inside the host's main system menu. Called once
    /// per frame while this system is selected.
    /// </summary>
    void Draw();
}
