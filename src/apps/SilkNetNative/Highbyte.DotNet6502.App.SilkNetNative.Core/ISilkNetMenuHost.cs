namespace Highbyte.DotNet6502.App.SilkNetNative.Core;

/// <summary>
/// Mutable menu-wide state that <see cref="IImGuiMenuContributor"/> implementations need
/// to share with the outer <c>SilkNetImGuiMenu</c>. Implemented by the menu itself and
/// handed to per-system contributors so the menu can render the error/state band once
/// at the bottom regardless of which system is selected.
/// </summary>
public interface ISilkNetMenuHost
{
    /// <summary>
    /// Last user-facing error from a file/disk operation. Cleared at the start of an
    /// operation; set on failure. Rendered by the outer menu in its error band.
    /// </summary>
    string LastFileError { get; set; }

    /// <summary>
    /// Request that the menu collapses on the next frame. Used after "Start"/"Load" so
    /// the menu gets out of the way without the contributor needing to know how to
    /// drive ImGui window state.
    /// </summary>
    bool DeferredCollapseWindow { get; set; }
}
