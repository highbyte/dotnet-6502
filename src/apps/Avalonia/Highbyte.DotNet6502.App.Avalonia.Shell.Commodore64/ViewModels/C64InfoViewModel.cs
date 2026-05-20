using System;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.ViewModels;

public class C64InfoViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _avaloniaHostApp;
    public AvaloniaHostApp HostApp => _avaloniaHostApp;


    public C64InfoViewModel(
        AvaloniaHostApp avaloniaHostApp)
    {
        _avaloniaHostApp = avaloniaHostApp ?? throw new ArgumentNullException(nameof(avaloniaHostApp));
    }
}
