using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.Views;

public partial class Vic20ConfigDialog : Window
{
    public bool? DialogResultValue { get; private set; }

    private static PixelPoint? s_lastPosition;
    private static Size? s_lastSize;
    private static WindowState? s_lastWindowState;
    private bool _isPositionInitialized;

    public Vic20ConfigDialog()
    {
        InitializeComponent();
        DialogResultValue = false;
        RestoreWindowBounds();
        Opened += OnOpened;
        PositionChanged += OnPositionChanged;
        Closed += OnClosed;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void RestoreWindowBounds()
    {
        if (s_lastSize.HasValue)
        {
            Width = s_lastSize.Value.Width;
            Height = s_lastSize.Value.Height;
        }

        if (s_lastPosition.HasValue)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = s_lastPosition.Value;
        }

        if (s_lastWindowState.HasValue && s_lastWindowState.Value != WindowState.Minimized)
            WindowState = s_lastWindowState.Value;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (s_lastPosition.HasValue && !_isPositionInitialized)
        {
            Position = s_lastPosition.Value;
            _isPositionInitialized = true;
        }
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (WindowState != WindowState.Minimized)
            s_lastPosition = e.Point;

        if (!_isPositionInitialized)
            _isPositionInitialized = true;
    }

    public void OnConfigurationChanged(object? sender, bool saved)
    {
        DialogResultValue = saved;
        Close(saved);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized && _isPositionInitialized)
        {
            s_lastSize = new Size(Width, Height);
            s_lastWindowState = WindowState;
        }

        Opened -= OnOpened;
        PositionChanged -= OnPositionChanged;
        Closed -= OnClosed;
    }
}
