using Terminal.Gui.ViewBase;

namespace Highbyte.DotNet6502.App.Terminal;

/// <summary>
/// Optional per-system info panel contributed by a shell plugin (<c>ISystemShellPlugin</c>). When a
/// discovered system provides one, the terminal host shows <see cref="View"/> in the Info tab of the
/// lower-right tabbed area (see <see cref="TuiHostApp"/>); systems without one get a generic
/// fallback. UI-framework-specific to the terminal host (Terminal.Gui); the plugin contract stays
/// UI-agnostic by returning <see cref="object"/> and letting the host cast to this type.
/// </summary>
public interface ITerminalInfoContribution
{
    /// <summary>The Terminal.Gui view rendered inside the Info tab content area.</summary>
    View View { get; }
}
