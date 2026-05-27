using System;
using System.Collections.ObjectModel;
using System.Linq;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia.Vic20;
using Highbyte.DotNet6502.Systems.Vic20.Config;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.ViewModels;

/// <summary>
/// Minimal config-dialog contribution for the VIC-20 shell plugin. Reads the current
/// <see cref="Vic20HostConfig"/> from the host and shows ROM directory / ROM file list
/// read-only. Kept tiny on purpose — the goal is to prove the plugin's config-dialog
/// contribution is reachable through the host, not to build a full configuration UI.
/// </summary>
public class Vic20ConfigDialogViewModel : ViewModelBase
{
    public Vic20ConfigDialogViewModel(AvaloniaHostApp hostApp)
    {
        if (hostApp == null) throw new ArgumentNullException(nameof(hostApp));

        var config = hostApp.CurrentHostSystemConfig as Vic20HostConfig;
        ROMDirectory = config?.SystemConfig.ROMDirectory ?? string.Empty;

        var roms = config?.SystemConfig.ROMs ?? new System.Collections.Generic.List<Highbyte.DotNet6502.Systems.ROM>();
        ROMs = new ObservableCollection<RomEntry>(
            roms.Select(r => new RomEntry(r.Name, r.File ?? string.Empty)));
    }

    public string ROMDirectory { get; }

    public ObservableCollection<RomEntry> ROMs { get; }

    public record RomEntry(string Name, string File);
}
