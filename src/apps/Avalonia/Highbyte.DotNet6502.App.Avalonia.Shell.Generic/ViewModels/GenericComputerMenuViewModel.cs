using System;
using System.Reactive;
using System.Threading.Tasks;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Systems;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Generic.ViewModels;

/// <summary>
/// Generic computer sidebar menu. The Generic machine has no media (no PRG/disk/tape),
/// so the menu's only job is hosting the Configuration section that opens the config
/// dialog.
/// </summary>
public class GenericComputerMenuViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _hostApp;
    private bool _isConfigSectionExpanded = true;

    public AvaloniaHostApp HostApp => _hostApp;

    public ReactiveCommand<Unit, Unit> ToggleConfigSectionCommand { get; }

    public GenericComputerMenuViewModel(AvaloniaHostApp hostApp)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));

        _hostApp
            .WhenAnyValue(x => x.EmulatorState)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsGenericConfigEnabled)));

        ToggleConfigSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                IsConfigSectionExpanded = !IsConfigSectionExpanded;
                return Task.CompletedTask;
            },
            outputScheduler: RxSchedulers.MainThreadScheduler);
    }

    public bool IsConfigSectionExpanded
    {
        get => _isConfigSectionExpanded;
        private set => this.RaiseAndSetIfChanged(ref _isConfigSectionExpanded, value);
    }

    /// <summary>The config dialog edits the system's construction-time settings, so it only opens while stopped.</summary>
    public bool IsGenericConfigEnabled => _hostApp.EmulatorState == EmulatorState.Uninitialized;
}
