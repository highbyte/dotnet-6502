using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Highbyte.DotNet6502.App.Avalonia.Core.Monitor;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class MonitorDialog : Window
{
    private readonly AvaloniaHostApp _hostApp;
    private readonly AvaloniaMonitor _monitor;

    // Static fields to remember window position and size across instances
    private static PixelPoint? s_lastPosition;
    private static Size? s_lastSize;
    private static WindowState? s_lastWindowState;
    private bool _isPositionInitialized = false;

    public MonitorDialog(AvaloniaHostApp hostApp, AvaloniaMonitor monitor)
    {
        _hostApp = hostApp;
        _monitor = monitor;

        InitializeComponent();

        var monitorControl = new MonitorUserControl(hostApp, monitor);
        Content = monitorControl;

        // Restore previous window position and size if available
        RestoreWindowBounds();

        // Subscribe to events
        Opened += OnOpened;
        PositionChanged += OnPositionChanged;
        Closed += OnClosed;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
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

        Opened -= OnOpened;
        PositionChanged -= OnPositionChanged;
        Closed -= OnClosed;

        if (_hostApp.IsMonitorVisible)
            _hostApp.DisableMonitor();
    }
}
