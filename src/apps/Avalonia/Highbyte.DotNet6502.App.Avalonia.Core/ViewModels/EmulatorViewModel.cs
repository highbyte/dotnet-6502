using System;
using System.ComponentModel;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class EmulatorViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _hostApp;

    public event EventHandler? RequestFocus;

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
                RequestFocus?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
