using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia.Vic20;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Vic20System = Highbyte.DotNet6502.Systems.Vic20.Vic20;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.ViewModels;

/// <summary>
/// Menu view model for the VIC-20 shell plugin.
/// </summary>
public class Vic20MenuViewModel : ViewModelBase, ISystemMenuContributor
{
    private readonly AvaloniaHostApp _hostApp;
    private readonly ILogger _logger;

    public AvaloniaHostApp HostApp => _hostApp;

    public ReactiveCommand<byte[], Unit> LoadBasicFileCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleLoadSaveSectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleConfigSectionCommand { get; }
    public ReactiveCommand<int, Unit> SetActiveJoystickCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleJoystickKeyboardCommand { get; }
    public ReactiveCommand<int, Unit> SetKeyboardJoystickCommand { get; }

    public Vic20MenuViewModel(AvaloniaHostApp hostApp, ILoggerFactory loggerFactory)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _logger = loggerFactory.CreateLogger(typeof(Vic20MenuViewModel).Name);

        InitializePlaceholderData();

        _hostApp
            .WhenAnyValue(x => x.EmulatorState)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsFileOperationEnabled));
                this.RaisePropertyChanged(nameof(IsVic20ConfigEnabled));
            });

        LoadBasicFileCommand = ReactiveCommandHelper.CreateSafeCommand<byte[]>(
            async (fileBuffer) => await LoadBasicFileAsync(fileBuffer),
            this.WhenAnyValue(x => x.IsFileOperationEnabled),
            RxSchedulers.MainThreadScheduler);

        ToggleLoadSaveSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () => ToggleSection(Vic20MenuSection.LoadSave),
            null,
            RxSchedulers.MainThreadScheduler);

        ToggleConfigSectionCommand = ReactiveCommandHelper.CreateSafeCommand(
            () => ToggleSection(Vic20MenuSection.Config),
            null,
            RxSchedulers.MainThreadScheduler);

        var placeholderCanExecute = this.WhenAnyValue(x => x.ArePlaceholderControlsEnabled);

        SetActiveJoystickCommand = ReactiveCommandHelper.CreateSafeCommand<int>(
            port => CurrentJoystick = port,
            placeholderCanExecute,
            RxSchedulers.MainThreadScheduler);

        ToggleJoystickKeyboardCommand = ReactiveCommandHelper.CreateSafeCommand(
            () => JoystickKeyboardEnabled = !JoystickKeyboardEnabled,
            placeholderCanExecute,
            RxSchedulers.MainThreadScheduler);

        SetKeyboardJoystickCommand = ReactiveCommandHelper.CreateSafeCommand<int>(
            port => KeyboardJoystick = port,
            placeholderCanExecute,
            RxSchedulers.MainThreadScheduler);
    }

    public bool IsFileOperationEnabled => _hostApp.EmulatorState != EmulatorState.Uninitialized;
    public bool IsVic20ConfigEnabled => _hostApp.EmulatorState == EmulatorState.Uninitialized;
    public bool ArePlaceholderControlsEnabled => false;

    public bool HasConfigValidationErrors
    {
        get
        {
            if (_hostApp.CurrentHostSystemConfig is not Vic20HostConfig vic20HostConfig)
                return false;

            return !vic20HostConfig.IsValid(out _);
        }
    }

    public ObservableCollection<KeyValuePair<string, string>> AssemblyExamples { get; } = new();
    private string _selectedAssemblyExample = string.Empty;
    public string SelectedAssemblyExample
    {
        get => _selectedAssemblyExample;
        set => this.RaiseAndSetIfChanged(ref _selectedAssemblyExample, value);
    }

    public ObservableCollection<KeyValuePair<string, string>> BasicExamples { get; } = new();
    private string _selectedBasicExample = string.Empty;
    public string SelectedBasicExample
    {
        get => _selectedBasicExample;
        set => this.RaiseAndSetIfChanged(ref _selectedBasicExample, value);
    }

    public ObservableCollection<int> AvailableJoysticks { get; } = new();

    private int _currentJoystick = 1;
    public int CurrentJoystick
    {
        get => _currentJoystick;
        set => this.RaiseAndSetIfChanged(ref _currentJoystick, value);
    }

    private bool _joystickKeyboardEnabled;
    public bool JoystickKeyboardEnabled
    {
        get => _joystickKeyboardEnabled;
        set
        {
            if (_joystickKeyboardEnabled == value)
                return;

            this.RaiseAndSetIfChanged(ref _joystickKeyboardEnabled, value);
            this.RaisePropertyChanged(nameof(IsKeyboardJoystickSelectionEnabled));
        }
    }

    private int _keyboardJoystick = 1;
    public int KeyboardJoystick
    {
        get => _keyboardJoystick;
        set => this.RaiseAndSetIfChanged(ref _keyboardJoystick, value);
    }

    public bool IsKeyboardJoystickSelectionEnabled => ArePlaceholderControlsEnabled && JoystickKeyboardEnabled;

    private bool _isLoadSaveSectionExpanded = true;
    public bool IsLoadSaveSectionExpanded
    {
        get => _isLoadSaveSectionExpanded;
        private set
        {
            if (_isLoadSaveSectionExpanded == value)
                return;
            _isLoadSaveSectionExpanded = value;
            this.RaisePropertyChanged(nameof(IsLoadSaveSectionExpanded));
            this.RaisePropertyChanged(nameof(LoadSaveSectionHeaderText));
        }
    }

    private bool _isConfigSectionExpanded;
    public bool IsConfigSectionExpanded
    {
        get => _isConfigSectionExpanded;
        private set
        {
            if (_isConfigSectionExpanded == value)
                return;
            _isConfigSectionExpanded = value;
            this.RaisePropertyChanged(nameof(IsConfigSectionExpanded));
            this.RaisePropertyChanged(nameof(ConfigSectionHeaderText));
        }
    }

    public string LoadSaveSectionHeaderText => "Load/Save";
    public string ConfigSectionHeaderText => "Configuration";

    private enum Vic20MenuSection { LoadSave, Config }

    private void ToggleSection(Vic20MenuSection section)
    {
        bool newState = section switch
        {
            Vic20MenuSection.LoadSave => !IsLoadSaveSectionExpanded,
            Vic20MenuSection.Config => !IsConfigSectionExpanded,
            _ => false,
        };

        SetSectionExpanded(section, newState, collapseOthers: newState);
    }

    private void SetSectionExpanded(Vic20MenuSection section, bool expanded, bool collapseOthers)
    {
        switch (section)
        {
            case Vic20MenuSection.LoadSave:
                IsLoadSaveSectionExpanded = expanded;
                if (collapseOthers && expanded)
                    IsConfigSectionExpanded = false;
                break;
            case Vic20MenuSection.Config:
                IsConfigSectionExpanded = expanded;
                if (collapseOthers && expanded)
                    IsLoadSaveSectionExpanded = false;
                break;
        }
    }

    public void ExpandConfigSectionOnValidationError()
    {
        IsLoadSaveSectionExpanded = false;
        IsConfigSectionExpanded = true;
    }

    public string MenuLabel => "VIC-20";

    public IReadOnlyList<NativeMenuItemBase> GetNativeMenuItems()
    {
        const KeyModifiers macBase = KeyModifiers.Meta | KeyModifiers.Alt;
        const KeyModifiers macShift = KeyModifiers.Meta | KeyModifiers.Alt | KeyModifiers.Shift;

        return new NativeMenuItemBase[]
        {
            BuildMenuItem("Toggle Load/Save section", new KeyGesture(Key.L, macShift), ToggleLoadSaveSectionCommand),
            BuildMenuItem("Toggle Configuration section", new KeyGesture(Key.C, macShift), ToggleConfigSectionCommand),
            new NativeMenuItemSeparator(),
            BuildMenuItem("Active joystick: Port 1", new KeyGesture(Key.D1, macBase), SetActiveJoystickCommand, 1),
            BuildMenuItem("Active joystick: Port 2", new KeyGesture(Key.D2, macBase), SetActiveJoystickCommand, 2),
            new NativeMenuItemSeparator(),
            BuildMenuItem("Toggle Joystick KB", new KeyGesture(Key.K, macBase), ToggleJoystickKeyboardCommand),
            BuildMenuItem("Keyboard joystick: Port 1", new KeyGesture(Key.D1, macShift), SetKeyboardJoystickCommand, 1),
            BuildMenuItem("Keyboard joystick: Port 2", new KeyGesture(Key.D2, macShift), SetKeyboardJoystickCommand, 2),
        };
    }

    public IReadOnlyList<KeyBinding> GetKeyBindings()
    {
        const KeyModifiers nonMacBase = KeyModifiers.Control | KeyModifiers.Alt;
        const KeyModifiers nonMacShift = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift;

        return new[]
        {
            BuildKeyBinding(new KeyGesture(Key.L, nonMacShift), ToggleLoadSaveSectionCommand),
            BuildKeyBinding(new KeyGesture(Key.C, nonMacShift), ToggleConfigSectionCommand),
            BuildKeyBinding(new KeyGesture(Key.D1, nonMacBase), SetActiveJoystickCommand, 1),
            BuildKeyBinding(new KeyGesture(Key.D2, nonMacBase), SetActiveJoystickCommand, 2),
            BuildKeyBinding(new KeyGesture(Key.K, nonMacBase), ToggleJoystickKeyboardCommand),
            BuildKeyBinding(new KeyGesture(Key.D1, nonMacShift), SetKeyboardJoystickCommand, 1),
            BuildKeyBinding(new KeyGesture(Key.D2, nonMacShift), SetKeyboardJoystickCommand, 2),
        };
    }

    private static NativeMenuItem BuildMenuItem(string header, KeyGesture gesture, System.Windows.Input.ICommand command, object? parameter = null)
    {
        var item = new NativeMenuItem
        {
            Header = header,
            Gesture = gesture,
            Command = command,
        };
        if (parameter != null)
            item.CommandParameter = parameter;
        return item;
    }

    private static KeyBinding BuildKeyBinding(KeyGesture gesture, System.Windows.Input.ICommand command, object? parameter = null)
    {
        var binding = new KeyBinding
        {
            Gesture = gesture,
            Command = command,
        };
        if (parameter != null)
            binding.CommandParameter = parameter;
        return binding;
    }

    private void InitializePlaceholderData()
    {
        AvailableJoysticks.Clear();
        AvailableJoysticks.Add(1);
        AvailableJoysticks.Add(2);

        AssemblyExamples.Clear();
        AssemblyExamples.Add(new KeyValuePair<string, string>(string.Empty, "-- Select an example --"));

        BasicExamples.Clear();
        BasicExamples.Add(new KeyValuePair<string, string>(string.Empty, "-- Select an example --"));
    }

    private async Task LoadBasicFileAsync(byte[] fileBuffer)
    {
        if (_hostApp.EmulatorState == EmulatorState.Uninitialized)
            return;

        bool wasRunning = _hostApp.EmulatorState == EmulatorState.Running;
        if (wasRunning)
            _hostApp.Pause();

        try
        {
            BinaryLoader.Load(
                _hostApp.CurrentRunningSystem!.Mem,
                fileBuffer,
                out ushort loadedAtAddress,
                out ushort fileLength);

            if (loadedAtAddress != Vic20System.BASIC_LOAD_ADDRESS)
            {
                _logger.LogWarning($"Loaded program is not a BASIC program; expected load address {Vic20System.BASIC_LOAD_ADDRESS.ToHex()} but got {loadedAtAddress.ToHex()}");
            }
            else
            {
                var vic20 = (Vic20System)_hostApp.CurrentRunningSystem!;
                vic20.InitBasicMemoryVariables(loadedAtAddress, fileLength);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading BASIC .prg");
        }
        finally
        {
            if (wasRunning)
                await _hostApp.Start();
        }
    }
}
