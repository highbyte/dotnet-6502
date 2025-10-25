using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Highbyte.DotNet6502.App.Avalonia.Core.Monitor;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class MonitorDialog : Window
{
    private readonly AvaloniaHostApp _hostApp;
    private readonly AvaloniaMonitor _monitor;

    public MonitorDialog(AvaloniaHostApp hostApp, AvaloniaMonitor monitor)
    {
        _hostApp = hostApp;
        _monitor = monitor;

        InitializeComponent();

        var monitorControl = new MonitorUserControl(hostApp, monitor);
        Content = monitorControl;

        Closed += OnClosed;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;

        if (_monitor.IsVisible)
            _hostApp.DisableMonitor();
    }
}
