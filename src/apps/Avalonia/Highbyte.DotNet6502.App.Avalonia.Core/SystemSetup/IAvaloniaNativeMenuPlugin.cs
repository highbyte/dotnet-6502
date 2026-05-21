namespace Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;

/// <summary>
/// Optional Avalonia-host capability for a shell plug-in: declares that the plug-in's menu
/// contribution also drives the macOS native menu bar and the Windows/Linux key bindings.
/// </summary>
/// <remarks>
/// A shell plug-in implements this <i>in addition to</i> <c>ISystemShellPlugin</c>. It is a
/// projection, not a second factory: the host (<c>MainViewModel</c>) resolves the plug-in's
/// menu contribution once per active-system change and caches it, then calls
/// <see cref="GetNativeMenuContributor"/> on that cached object. This guarantees the native
/// menu, the in-window menu panel, and the running emulator all share the same ViewModel
/// instance — a separate factory call could mint a disconnected VM whose commands appear
/// to do nothing.
///
/// <see cref="ISystemShellPlugin"/> itself cannot expose this, because
/// <see cref="ISystemMenuContributor"/> is built on Avalonia types and the shared plug-in
/// contract is host-technology agnostic.
/// </remarks>
public interface IAvaloniaNativeMenuPlugin
{
    /// <summary>
    /// Project this plug-in's menu contribution onto its native-menu / keyboard-shortcut
    /// surface, or return null if it contributes no native menu.
    /// </summary>
    /// <param name="menuContribution">
    /// The object the host previously obtained from <c>ISystemShellPlugin.CreateMenuContribution</c>
    /// and is using as the in-window menu panel's DataContext. The host passes its own cached
    /// instance in (rather than letting this method resolve a new one) so that the native menu
    /// and the in-window panel are driven by the <i>same</i> ViewModel — the implementation
    /// must project this argument, not create a fresh instance. May be null if the active
    /// plug-in produced no menu contribution.
    /// </param>
    /// <returns>
    /// The native-menu surface of <paramref name="menuContribution"/> — typically the same
    /// object cast to <see cref="ISystemMenuContributor"/> — or null when it contributes none.
    /// </returns>
    ISystemMenuContributor? GetNativeMenuContributor(object? menuContribution);
}
