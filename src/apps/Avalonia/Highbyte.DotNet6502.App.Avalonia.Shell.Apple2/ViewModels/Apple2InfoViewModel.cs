using System;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Systems.Apple2.Config;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Apple2.ViewModels;

/// <summary>
/// Info-panel contribution for the Apple II shell plugin: a few static system facts to accompany
/// the keyboard-mapping table in the view.
/// </summary>
public class Apple2InfoViewModel : ViewModelBase
{
    public Apple2InfoViewModel(AvaloniaHostApp hostApp)
    {
        if (hostApp == null) throw new ArgumentNullException(nameof(hostApp));
    }

    public string SystemName => global::Highbyte.DotNet6502.Systems.Apple2.Apple2.SystemName;

    public string TextMode => $"{Apple2Config.Cols} × {Apple2Config.Rows} text mode, uppercase only";

    public string RefreshRate => "~59.92 Hz (17,030 cycles/frame)";

    public string MemoryLayout => "RAM $0000-$BFFF, soft switches $C000-$C0FF, ROM $D000-$FFFF";

    public string RequiredROMs => string.Join(", ", Apple2SystemConfig.RequiredROMs);
}
