using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Highbyte.DotNet6502.App.Avalonia.Shell.Generic.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Generic.Views;

public partial class GenericComputerConfigDialogView : UserControl
{
    private GenericComputerConfigDialogViewModel? _previousViewModel;
    private GenericComputerConfigDialogViewModel? ViewModel => DataContext as GenericComputerConfigDialogViewModel;
    private EventHandler<bool>? _configurationChangedHandlers;

    // Forwards to the view model's event, resubscribing when the DataContext changes so
    // handlers added before the DataContext is set still fire.
    public event EventHandler<bool>? ConfigurationChanged
    {
        add
        {
            _configurationChangedHandlers += value;
            if (ViewModel != null)
                ViewModel.ConfigurationChanged += value;
        }
        remove
        {
            _configurationChangedHandlers -= value;
            if (ViewModel != null)
                ViewModel.ConfigurationChanged -= value;
        }
    }

    public GenericComputerConfigDialogView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_previousViewModel != null && _configurationChangedHandlers != null)
            _previousViewModel.ConfigurationChanged -= _configurationChangedHandlers;

        if (ViewModel != null && _configurationChangedHandlers != null)
            ViewModel.ConfigurationChanged += _configurationChangedHandlers;

        _previousViewModel = ViewModel;
    }
}
