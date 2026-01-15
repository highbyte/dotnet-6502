using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia.Monitor;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class MonitorDialog : Window
{
    // Static fields to remember window position and size across instances
    private static PixelPoint? s_lastPosition;
    private static Size? s_lastSize;
    private static WindowState? s_lastWindowState;
    private bool _isPositionInitialized = false;
    private AvaloniaMonitor? _monitor;

    public MonitorDialog()
    {
        InitializeComponent();

        // Restore previous window position and size if available
        RestoreWindowBounds();

        // Subscribe to events
        Opened += OnOpened;
        PositionChanged += OnPositionChanged;
        Closed += OnClosed;
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // When DataContext is set to a MonitorViewModel, create and add the MonitorUserControl
        if (DataContext is MonitorViewModel viewModel)
        {
            var monitorControl = new MonitorUserControl(viewModel);
            Content = monitorControl;

            // Subscribe to Monitor's PropertyChanged event to close when IsVisible becomes false
            _monitor = viewModel.Monitor;
            if (_monitor != null)
                _monitor.PropertyChanged += OnMonitorPropertyChanged;
        }
    }

    private void OnMonitorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AvaloniaMonitor.IsVisible) && _monitor?.IsVisible == false)
        {
            Close();
        }
    }

    private void RestoreWindowBounds()
    {
        // Restore size immediately
        if (s_lastSize.HasValue)
        {
            Width = s_lastSize.Value.Width;
            Height = s_lastSize.Value.Height;
        }

        // Set WindowStartupLocation to Manual if we have a saved position
        if (s_lastPosition.HasValue)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            // Try setting position immediately as well
            Position = s_lastPosition.Value;
        }

        if (s_lastWindowState.HasValue && s_lastWindowState.Value != WindowState.Minimized)
        {
            WindowState = s_lastWindowState.Value;
        }
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        // Restore position after window is opened (for modal dialogs)
        if (s_lastPosition.HasValue && !_isPositionInitialized)
        {
            Position = s_lastPosition.Value;
            _isPositionInitialized = true;
        }
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        // Save the new position whenever it changes
        if (WindowState != WindowState.Minimized)
        {
            s_lastPosition = e.Point;
        }

        // Mark position as initialized once it changes (after the window is positioned)
        if (!_isPositionInitialized)
        {
            _isPositionInitialized = true;
        }
    }

    private void SaveWindowBounds()
    {
        // Save current size and window state for next time
        // Note: Position is already saved in OnPositionChanged event handler
        if (WindowState != WindowState.Minimized && _isPositionInitialized)
        {
            s_lastSize = new Size(Width, Height);
            s_lastWindowState = WindowState;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        // Save window bounds before closing
        SaveWindowBounds();

        // Unsubscribe from Monitor event if it exists
        if (_monitor != null)
        {
            _monitor.PropertyChanged -= OnMonitorPropertyChanged;
            _monitor = null;
        }

        Opened -= OnOpened;
        PositionChanged -= OnPositionChanged;
        Closed -= OnClosed;
        DataContextChanged -= OnDataContextChanged;
    }
}
