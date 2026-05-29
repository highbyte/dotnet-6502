using System;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia.Vic20;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.ViewModels;

/// <summary>
/// Minimal menu contribution for the VIC-20 shell plugin.
/// </summary>
public class Vic20MenuViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _hostApp;

    public AvaloniaHostApp HostApp => _hostApp;

    public Vic20MenuViewModel(AvaloniaHostApp hostApp)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
    }

    public bool HasConfigValidationErrors
    {
        get
        {
            if (_hostApp.CurrentHostSystemConfig is not Vic20HostConfig vic20HostConfig)
                return false;

            return !vic20HostConfig.IsValid(out _);
        }
    }
}
