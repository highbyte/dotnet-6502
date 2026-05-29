using System;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Systems.Vic20.Config;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.ViewModels;

/// <summary>
/// Minimal info-panel contribution for the VIC-20 shell plugin. Surfaces a handful of
/// static system facts so the plugin's info contribution is non-null and bindable through
/// the host's ViewLocator. Not a documentation page — its purpose is to prove the
/// contribution path works.
/// </summary>
public class Vic20InfoViewModel : ViewModelBase
{
    public Vic20InfoViewModel(AvaloniaHostApp hostApp)
    {
        if (hostApp == null) throw new ArgumentNullException(nameof(hostApp));
    }

    public string SystemName => global::Highbyte.DotNet6502.Systems.Vic20.Vic20.SystemName;

    public string TextMode => $"{Vic20Config.Cols} × {Vic20Config.Rows} text mode";

    public string RefreshRate => "NTSC, ~60 Hz";

    public string MemoryLayout => "Screen RAM $1E00, color RAM $9600, BASIC $C000, KERNAL $E000";

    public string RequiredROMs => string.Join(", ", Vic20SystemConfig.RequiredROMs);
}
