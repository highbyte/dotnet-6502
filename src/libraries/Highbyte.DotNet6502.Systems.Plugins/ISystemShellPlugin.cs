using Microsoft.Extensions.DependencyInjection;

namespace Highbyte.DotNet6502.Systems.Plugins;

/// <summary>
/// Shell-side plugin: ships in <c>App.&lt;Tech&gt;.Shell.&lt;System&gt;</c> libraries.
/// Contributes per-system UI pieces (ViewModels, Views, menu items, config dialogs)
/// to a host app's shell, keyed by <see cref="SystemName"/>.
/// </summary>
/// <remarks>
/// Returns <see cref="object"/> deliberately: this contract is UI-framework-agnostic so
/// the same interface serves Avalonia (ViewModelBase), SadConsole (consoles), SilkNet
/// (ImGui windows), and Blazor (component types). Each host app casts to its own type.
/// </remarks>
public interface ISystemShellPlugin
{
    string SystemName { get; }

    /// <summary>
    /// Register the plugin's ViewModels / Views / helper services in DI.
    /// Called once at app startup before the shell resolves anything.
    /// </summary>
    void RegisterShellServices(IServiceCollection services);

    /// <summary>
    /// Resolve the plugin's per-system menu/sidebar ViewModel from the DI scope.
    /// May return <c>null</c> if the system has no menu contribution.
    /// </summary>
    object? CreateMenuContribution(IServiceProvider serviceProvider);

    /// <summary>
    /// Resolve the plugin's info-panel ViewModel from the DI scope.
    /// </summary>
    object? CreateInfoContribution(IServiceProvider serviceProvider);

    /// <summary>
    /// Resolve the plugin's config-dialog ViewModel from the DI scope.
    /// </summary>
    object? CreateConfigDialogContribution(IServiceProvider serviceProvider);
}
