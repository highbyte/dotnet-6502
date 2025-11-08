using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Highbyte.DotNet6502.App.Avalonia.Core.Controls;
using Highbyte.DotNet6502.App.Avalonia.Core.Render;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

/// <summary>
/// View for displaying C64 emulator using WriteableBitmap-based rendering.
/// This view is optimized for WebAssembly and cross-platform compatibility.
/// </summary>
public partial class EmulatorView : UserControl
{
    private EmulatorViewModel? _subscribedViewModel;

    private EmulatorDisplayControlBase? _renderControl;

    public static readonly StyledProperty<double> ScaleProperty =
        AvaloniaProperty.Register<EmulatorView, double>(nameof(Scale), 2.0);

    public double Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

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

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from previous ViewModel's events and property changes
        if (_subscribedViewModel != null)
        {
            //_subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedViewModel.RequestFocus -= OnRequestFocus;
            _subscribedViewModel.RequestRenderConfiguration -= OnRequestRenderConfiguration;
        }

        // Subscribe to new ViewModel's events and property changes
        _subscribedViewModel = DataContext as EmulatorViewModel;
        if (_subscribedViewModel != null)
        {
            //_subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            _subscribedViewModel.RequestFocus += OnRequestFocus;
            _subscribedViewModel.RequestRenderConfiguration += OnRequestRenderConfiguration;

            // Note: We don't register the render control here because it's null at this point.
            // Registration happens in OnRequestRenderConfiguration after the control is created.
        }
    }

    private void OnRequestFocus(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Focus();
        }, DispatcherPriority.Background);
    }

    private void OnRequestRenderConfiguration(object? sender, RenderConfigurationEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Remove existing control if any
            if (_renderControl != null)
            {
                RenderingControlContainer.Content = null;
                _renderControl = null;
            }

            var renderControl = CreateRendererControl(e.RenderCoordinator, e.AvaloniaBitmapRenderTarget);
            renderControl.SetDisplaySize(e.Screen.VisibleWidth, e.Screen.VisibleHeight);

            // Set the new control to be rendered as content
            RenderingControlContainer.Content = renderControl;

            // Remember the render control
            _renderControl = renderControl;

            // Register the render control with HostApp after it's been created
            if (HostApp != null && _renderControl != null)
            {
                HostApp.RegisterRenderControl(_renderControl);
            }
        }, DispatcherPriority.Background);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ScaleProperty)
        {
            // Update the render control's scale when this control's scale changes
            if (_renderControl != null)
                _renderControl.Scale = Scale;
        }
    }

    private EmulatorDisplayControlBase CreateRendererControl(IRenderCoordinator? renderCoordinator, IAvaloniaBitmapRenderTarget? avaloniaBitmapRenderTarget)
    {
        EmulatorDisplayControlBase renderControl;
        // Check if we have an Avalonia command target instead of bitmap target
        var avaloniaCommandTarget = HostApp?.GetRenderTarget<ICommandTarget>();
        if (avaloniaCommandTarget is AvaloniaCommandTarget commandTarget)
        {
            renderControl = CreateAvaloniaCommandControl(renderCoordinator, commandTarget);
        }
        else
        {
            // Use bitmap display control for other targets
            renderControl = CreateBitmapDisplayControl(renderCoordinator, avaloniaBitmapRenderTarget);
        }

        // Apply current scale to the new control
        renderControl.Scale = Scale;
        return renderControl;
    }

    private EmulatorBitmapDisplayControl CreateBitmapDisplayControl(IRenderCoordinator? renderCoordinator, IAvaloniaBitmapRenderTarget? avaloniaBitmapRenderTarget)
    {
        var control = new EmulatorBitmapDisplayControl(
            renderCoordinator,
            avaloniaBitmapRenderTarget,
            Scale,  // Use current Scale value
            true,
            () => HostApp.EmulatorState == EmulatorState.Running);
        control.SetDisplaySize(320, 200);
        return control;
    }

    private EmulatorAvaloniaCommandControl CreateAvaloniaCommandControl(IRenderCoordinator? renderCoordinator, AvaloniaCommandTarget avaloniaCommandTarget)
    {
        var control = new EmulatorAvaloniaCommandControl(
            renderCoordinator,
            avaloniaCommandTarget,
            Scale,  // Use current Scale value
            true,
            () => HostApp.EmulatorState == EmulatorState.Running);
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
        HostApp?.OnKeyDown(e.Key, e.KeyModifiers);
    }

    /// <summary>
    /// Handle key up events and forward them to the host app
    /// </summary>
    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        // Prevent keys from being processed by Avalonia's focus system
        e.Handled = true;
        HostApp?.OnKeyUp(e.Key, e.KeyModifiers);
    }

    /// <summary>
    /// Handle pointer pressed events to ensure the control gets focus
    /// </summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
    }
}
