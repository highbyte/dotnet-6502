using System;
using System.Reactive;
using System.Threading.Tasks;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Systems;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.ViewModels;

/// <summary>
/// Minimal menu contribution for the VIC-20 shell plugin. Exposes a Reset command so the
/// plugin's menu surface is non-null and demonstrably wired to the running system through
/// the shared <see cref="AvaloniaHostApp"/>. Kept intentionally tiny — this is the proof
/// that <c>ISystemShellPlugin.CreateMenuContribution</c> and the host's ViewLocator /
/// ContentControl binding work end-to-end, not a polished menu.
/// </summary>
public class Vic20MenuViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _hostApp;

    public AvaloniaHostApp HostApp => _hostApp;

    public ReactiveCommand<Unit, Unit> ResetCommand { get; }

    public Vic20MenuViewModel(AvaloniaHostApp hostApp)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));

        ResetCommand = ReactiveCommand.CreateFromTask(
            ResetAsync,
            this.WhenAnyValue(x => x.IsResetEnabled));

        _hostApp.WhenAnyValue(x => x.EmulatorState)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsResetEnabled)));
    }

    public bool IsResetEnabled =>
        _hostApp.EmulatorState == EmulatorState.Running ||
        _hostApp.EmulatorState == EmulatorState.Paused;

    private async Task ResetAsync()
    {
        if (!IsResetEnabled)
            return;
        await _hostApp.Reset();
    }
}
