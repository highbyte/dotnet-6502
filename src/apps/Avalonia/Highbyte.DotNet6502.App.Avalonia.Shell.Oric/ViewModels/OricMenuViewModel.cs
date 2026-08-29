using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Systems;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Oric.ViewModels;

public sealed class OricMenuViewModel : ViewModelBase
{
    public OricMenuViewModel(AvaloniaHostApp hostApp)
    {
        HostApp = hostApp;
        hostApp.WhenAnyValue(app => app.EmulatorState)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsConfigEnabled)));
    }

    public AvaloniaHostApp HostApp { get; }
    public bool IsConfigEnabled => HostApp.EmulatorState == EmulatorState.Uninitialized;
}
