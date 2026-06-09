using System;
using System.ComponentModel;
using Highbyte.DotNet6502.Impl.Avalonia.Render;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Rendering;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class EmulatorViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _hostApp;

    public event EventHandler? RequestFocus;
    public event EventHandler<RenderConfigurationEventArgs>? RequestRenderConfiguration;

    /// <summary>
    /// Raised when the emulator has stopped, so the view can clear the (now stale) render control.
    /// Prevents the previous system's last frame from briefly showing when a new system is started.
    /// </summary>
    public event EventHandler? RequestClearDisplay;

    public EmulatorViewModel(AvaloniaHostApp hostApp)
    {
        _hostApp = hostApp;
        _hostApp.PropertyChanged += OnHostAppPropertyChanged;
    }

    private void OnHostAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AvaloniaHostApp.EmulatorState))
        {
            if (_hostApp.EmulatorState == EmulatorState.Running)
            {
                var currentRunningSystem = _hostApp.CurrentRunningSystem;
                if (currentRunningSystem == null)
                    return;

                // When the CurrentRunningSystem property changes (system started), trigger render configuration
                var args = new RenderConfigurationEventArgs(
                    _hostApp.GetRenderCoordinator(),
                    _hostApp.GetRenderTarget<IAvaloniaBitmapRenderTarget>(),
                    currentRunningSystem.Screen
                );
                RequestRenderConfiguration?.Invoke(this, args);

                // Also make sure the view has focus to receive keyboard input
                RequestFocus?.Invoke(this, EventArgs.Empty);
            }
            else if (_hostApp.EmulatorState == EmulatorState.Uninitialized)
            {
                // Emulator stopped: drop the stale render control so its last frame isn't shown
                // when the view becomes visible again on the next start.
                RequestClearDisplay?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}

/// <summary>
/// Event args for rendering configuration that provides the necessary rendering components to the view.
/// </summary>
public class RenderConfigurationEventArgs : EventArgs
{
    public IRenderCoordinator? RenderCoordinator { get; }
    public IAvaloniaBitmapRenderTarget? AvaloniaBitmapRenderTarget { get; }
    public IScreen Screen { get; }

    public RenderConfigurationEventArgs(
        IRenderCoordinator? renderCoordinator,
        IAvaloniaBitmapRenderTarget? avaloniaBitmapRenderTarget,
        IScreen screen)
    {
        RenderCoordinator = renderCoordinator;
        AvaloniaBitmapRenderTarget = avaloniaBitmapRenderTarget;
        Screen = screen;
    }
}
