using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Apple2.ViewModels;

/// <summary>
/// Menu/sidebar contribution for the Apple II shell plugin.
///
/// Deliberately small: the Apple II has no PRG format, disk, tape or joystick support, so unlike
/// the C64 and VIC-20 menus there is nothing to load or save. Its job is to host the
/// Configuration section — which is also the only route to the ROM settings dialog.
/// </summary>
public class Apple2MenuViewModel : ViewModelBase, ISystemMenuContributor
{
    private readonly AvaloniaHostApp _hostApp;
    private readonly ILogger _logger;

    private bool _isConfigSectionExpanded = true;

    public AvaloniaHostApp HostApp => _hostApp;

    public ReactiveCommand<Unit, Unit> ToggleConfigSectionCommand { get; }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "ReactiveCommand usage is limited to application-defined view models rooted by the host application.")]
    public Apple2MenuViewModel(AvaloniaHostApp hostApp, ILoggerFactory loggerFactory)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _logger = loggerFactory.CreateLogger(nameof(Apple2MenuViewModel));

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
        set => this.RaiseAndSetIfChanged(ref _isConfigSectionExpanded, value);
    }

    /// <summary>Configuration may only be edited while the emulator is not running.</summary>
    public bool IsApple2ConfigEnabled => _hostApp.EmulatorState == EmulatorState.Uninitialized;

    public void RaiseEmulatorStateChanged() => this.RaisePropertyChanged(nameof(IsApple2ConfigEnabled));

    // --- ISystemMenuContributor ---

    public string MenuLabel => "Apple II";

    public IReadOnlyList<NativeMenuItemBase> GetNativeMenuItems()
    {
        const KeyModifiers macShift = KeyModifiers.Meta | KeyModifiers.Alt | KeyModifiers.Shift;

        return new NativeMenuItemBase[]
        {
            BuildMenuItem("Toggle Configuration section", new KeyGesture(Key.C, macShift), ToggleConfigSectionCommand),
        };
    }

    public IReadOnlyList<KeyBinding> GetKeyBindings()
    {
        const KeyModifiers nonMacShift = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift;

        return new[]
        {
            BuildKeyBinding(new KeyGesture(Key.C, nonMacShift), ToggleConfigSectionCommand),
        };
    }

    private static NativeMenuItem BuildMenuItem(string header, KeyGesture gesture, ICommand command)
        => new(header)
        {
            Gesture = gesture,
            Command = command,
        };

    private static KeyBinding BuildKeyBinding(KeyGesture gesture, ICommand command)
        => new()
        {
            Gesture = gesture,
            Command = command,
        };
}
