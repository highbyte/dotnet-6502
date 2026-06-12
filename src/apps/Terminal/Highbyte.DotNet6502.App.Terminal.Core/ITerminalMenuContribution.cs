using Terminal.Gui.ViewBase;

namespace Highbyte.DotNet6502.App.Terminal;

/// <summary>
/// Optional per-system menu control contributed by a shell plugin (<c>ISystemShellPlugin</c>).
/// When a discovered system provides one, the terminal host shows <see cref="View"/> in the controls
/// column below the standard controls (see <see cref="TuiHostApp"/>). UI-framework-specific to the
/// terminal host (Terminal.Gui); the plugin contract itself stays UI-agnostic by returning
/// <see cref="object"/> and letting the host cast to this type.
/// </summary>
public interface ITerminalMenuContribution
{
    /// <summary>Short heading shown above the control (e.g. the system name).</summary>
    string MenuTitle { get; }

    /// <summary>Number of rows the control needs in the controls column (excluding the border).</summary>
    int MenuRowCount { get; }

    /// <summary>The Terminal.Gui view hosting the system-specific controls.</summary>
    View View { get; }

    /// <summary>
    /// Refresh the enabled/disabled state of the contributed controls to match the current emulator
    /// state (so controls that can't be used right now render dimmed, like the standard host buttons).
    /// The host calls this whenever the emulator state or config changes.
    /// </summary>
    void RefreshControlStates();
}
