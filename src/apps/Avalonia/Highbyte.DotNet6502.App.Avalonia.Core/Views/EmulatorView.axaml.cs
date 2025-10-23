using System;
using Avalonia.Controls;
using Avalonia.Input;
using Highbyte.DotNet6502.App.Avalonia.Core.Controls;
using Highbyte.DotNet6502.App.Avalonia.Core.Render;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

/// <summary>
/// View for displaying C64 emulator using WriteableBitmap-based rendering.
/// This view is optimized for WebAssembly and cross-platform compatibility.
/// </summary>
public partial class EmulatorView : UserControl
{
    private EmulatorDisplayControlBase? _renderControl;

    // Access HostApp through ViewModel
    private AvaloniaHostApp? HostApp
    {
        get
        {
            // Navigate up the visual tree to find MainView's DataContext
            var parent = this.Parent;
            while (parent != null)
            {
                if (parent is MainView mainView && mainView.DataContext is MainViewModel mainViewModel)
                {
                    return mainViewModel.HostApp;
                }
                if (parent is Control control)
                    parent = control.Parent;
                else
                    break;
            }
            return null;
        }
    }

    public EmulatorDisplayControlBase? RenderControl => _renderControl;

    public EmulatorView()
    {
        InitializeComponent();

        // NO Init() method needed!
        // Wire up keyboard events
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;

        // Wire up pointer events to handle focus
        PointerPressed += OnPointerPressed;

        // Make the control focusable so it can receive keyboard events
        Focusable = true;
        IsTabStop = true;
    }

    public void ConfigureRendererControl(IRenderCoordinator? renderCoordinator, IAvaloniaBitmapRenderTarget? avaloniaBitmapRenderTarget)
    {
        // Remove existing control if any
        if (_renderControl != null)
        {
            RenderingControlContainer.Content = null;
            _renderControl = null;
        }

        // Check if we have an Avalonia command target instead of bitmap target
        var avaloniaCommandTarget = HostApp?.GetRenderTarget<ICommandTarget>();
        if (avaloniaCommandTarget is AvaloniaCommandTarget commandTarget)
        {
            _renderControl = CreateAvaloniaCommandControl(renderCoordinator, commandTarget);
        }
        else
        {
            // Use bitmap display control for other targets
            _renderControl = CreateBitmapDisplayControl(renderCoordinator, avaloniaBitmapRenderTarget);
        }

        // Set the new control
        RenderingControlContainer.Content = _renderControl;
    }

    private EmulatorBitmapDisplayControl CreateBitmapDisplayControl(IRenderCoordinator? renderCoordinator, IAvaloniaBitmapRenderTarget? avaloniaBitmapRenderTarget)
    {
        var control = new EmulatorBitmapDisplayControl(
     renderCoordinator,
       avaloniaBitmapRenderTarget,
            2.0,
        true);
        control.SetDisplaySize(320, 200);
        return control;
    }

    private EmulatorAvaloniaCommandControl CreateAvaloniaCommandControl(IRenderCoordinator? renderCoordinator, AvaloniaCommandTarget avaloniaCommandTarget)
    {
        var control = new EmulatorAvaloniaCommandControl(
            renderCoordinator,
            avaloniaCommandTarget,
         2.0,
   true);
        control.SetDisplaySize(320, 200);
        return control;
    }

    /// <summary>
    /// Handle key down events and forward them to the host app
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Prevent keys from being processed by Avalonia's focus system
        e.Handled = true;
        HostApp?.OnKeyDown(e.Key);
    }

    /// <summary>
    /// Handle key up events and forward them to the host app
    /// </summary>
    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        // Prevent keys from being processed by Avalonia's focus system
        e.Handled = true;
        HostApp?.OnKeyUp(e.Key);
    }

    /// <summary>
    /// Handle pointer pressed events to ensure the control gets focus
    /// </summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
    }
}
