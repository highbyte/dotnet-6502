using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class EmulatorConfigUserControl : UserControl
{
    private EmulatorConfigViewModel? _previousViewModel;
    private EmulatorConfigViewModel? ViewModel => DataContext as EmulatorConfigViewModel;
    private EventHandler<bool>? _configurationChangedHandlers;

    public event EventHandler<bool>? ConfigurationChanged
    {
        add
        {
            _configurationChangedHandlers += value;
            // If ViewModel is already available, subscribe immediately
            ViewModel?.ConfigurationChanged += value;
        }
        remove
        {
            _configurationChangedHandlers -= value;
            // Unsubscribe from ViewModel if available
            ViewModel?.ConfigurationChanged -= value;
        }
    }

    public EmulatorConfigUserControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from previous ViewModel
        if (_previousViewModel != null)
        {
            if (_configurationChangedHandlers != null)
                _previousViewModel.ConfigurationChanged -= _configurationChangedHandlers;
        }

        // Subscribe to new ViewModel
        if (ViewModel != null)
        {
            if (_configurationChangedHandlers != null)
                ViewModel.ConfigurationChanged += _configurationChangedHandlers;
        }

        _previousViewModel = ViewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
