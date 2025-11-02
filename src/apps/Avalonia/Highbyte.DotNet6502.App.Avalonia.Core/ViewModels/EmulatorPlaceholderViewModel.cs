using System;
using System.ComponentModel;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class EmulatorPlaceholderViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _hostApp;
    public IScreen? ScreenInfo => _hostApp.CurrentSystemScreenInfo;

    public event EventHandler? SizeChanged;

    public EmulatorPlaceholderViewModel(AvaloniaHostApp hostApp)
    {
        _hostApp = hostApp;
        _hostApp.PropertyChanged += OnHostAppPropertyChanged;
    }

    private void OnHostAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AvaloniaHostApp.SelectedSystemName)
            || e.PropertyName == nameof(AvaloniaHostApp.SelectedSystemConfigurationVariant))
        {
            SizeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
