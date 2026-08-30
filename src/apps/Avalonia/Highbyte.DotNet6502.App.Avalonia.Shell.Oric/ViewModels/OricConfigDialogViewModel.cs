using System.Collections.ObjectModel;
using System.Reactive;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Impl.Avalonia.Oric;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Oric.Config;
using Highbyte.DotNet6502.Systems.Oric.Input;
using Highbyte.DotNet6502.Utils;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Oric.ViewModels;

public sealed class OricConfigDialogViewModel : ViewModelBase
{
    private const string AutoKeyboardLayoutLabel = "Auto";

    private readonly AvaloniaHostApp _hostApp;
    private readonly OricHostConfig _originalConfig;
    private readonly OricHostConfig _workingConfig;
    private readonly HttpClient _httpClient = new();
    private bool _isBusy;
    private string _selectedKeyboardLayout = AutoKeyboardLayoutLabel;
    private string _statusMessage = string.Empty;

    public OricConfigDialogViewModel(AvaloniaHostApp hostApp)
    {
        _hostApp = hostApp;
        _originalConfig = hostApp.CurrentHostSystemConfig as OricHostConfig
            ?? throw new InvalidOperationException("Current host config must be OricHostConfig.");
        _workingConfig = (OricHostConfig)_originalConfig.Clone();
        SelectedKeyboardLayout =
            _workingConfig.InputConfig.KeyboardLayout?.ToString() ?? AutoKeyboardLayoutLabel;
        SelectAvailableTargets();
        InitializeJoystickOptions();

        DownloadCommand = ReactiveCommandHelper.CreateSafeCommand(DownloadAsync,
            outputScheduler: RxSchedulers.MainThreadScheduler);
        ClearCommand = ReactiveCommandHelper.CreateSafeCommand(() =>
        {
            _workingConfig.SystemConfig.ROMs = [];
            StatusMessage = "ROM cleared.";
            RaiseRomProperties();
            return Task.CompletedTask;
        }, outputScheduler: RxSchedulers.MainThreadScheduler);
        SaveCommand = ReactiveCommandHelper.CreateSafeCommand(SaveAsync,
            outputScheduler: RxSchedulers.MainThreadScheduler);
        CancelCommand = ReactiveCommandHelper.CreateSafeCommand(() =>
        {
            ConfigurationChanged?.Invoke(this, false);
            return Task.CompletedTask;
        }, outputScheduler: RxSchedulers.MainThreadScheduler);
    }

    public event EventHandler<bool>? ConfigurationChanged;
    public event EventHandler<OricRomLicenseAcknowledgementEventArgs>? RomLicenseAcknowledgementRequested;

    public ReactiveCommand<Unit, Unit> DownloadCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public bool IsRunningInWebAssembly { get; } = PlatformDetection.IsRunningInWebAssembly();
    public bool IsNotBusy => !_isBusy;
    public bool RomLoaded
    {
        get
        {
            if (!_workingConfig.SystemConfig.HasROM(OricSystemConfig.SystemRomName))
                return false;
            return _workingConfig.SystemConfig.GetROM(OricSystemConfig.SystemRomName)
                .Validate(out _, _workingConfig.SystemConfig.EffectiveROMDirectory);
        }
    }
    public string RomStatus => RomLoaded ? "Atmos BASIC 1.1b ROM is available" : "Atmos BASIC ROM is missing or invalid";
    public string RomSourceUrl => OricSystemConfig.RomSourceInfoUrl;
    public string EffectiveRomDirectory => PathHelper.ExpandOSEnvironmentVariables(_workingConfig.SystemConfig.EffectiveROMDirectory);

    public string RomDirectory
    {
        get => _workingConfig.SystemConfig.ROMDirectory;
        set
        {
            if (_workingConfig.SystemConfig.ROMDirectory == value)
                return;
            _workingConfig.SystemConfig.ROMDirectory = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(EffectiveRomDirectory));
        }
    }

    public bool AudioEnabled
    {
        get => _workingConfig.SystemConfig.AudioEnabled;
        set
        {
            _workingConfig.SystemConfig.AudioEnabled = value;
            this.RaisePropertyChanged();
        }
    }

    public ObservableCollection<KeyValuePair<OricJoystickInterface, string>> JoystickInterfaces { get; } = new();
    public ObservableCollection<int> AvailableJoysticks { get; } = new();
    public ObservableCollection<string> AvailableKeyboardLayouts { get; } =
        new(new[] { AutoKeyboardLayoutLabel }.Concat(Enum.GetNames<HostKeyboardLayout>()));

    public string SelectedKeyboardLayout
    {
        get => _selectedKeyboardLayout;
        set
        {
            if (_selectedKeyboardLayout == value)
                return;
            this.RaiseAndSetIfChanged(ref _selectedKeyboardLayout, value);
            _workingConfig.InputConfig.KeyboardLayout =
                string.IsNullOrEmpty(value) || value == AutoKeyboardLayoutLabel
                    ? null
                    : Enum.Parse<HostKeyboardLayout>(value);
        }
    }

    public OricJoystickInterface JoystickInterface
    {
        get => _workingConfig.SystemConfig.JoystickInterface;
        set
        {
            if (_workingConfig.SystemConfig.JoystickInterface == value)
                return;
            _workingConfig.SystemConfig.JoystickInterface = value;
            this.RaisePropertyChanged();
        }
    }

    public int HostJoystick
    {
        get => _workingConfig.InputConfig.CurrentJoystick;
        set
        {
            if (_workingConfig.InputConfig.CurrentJoystick == value)
                return;
            _workingConfig.InputConfig.CurrentJoystick = value;
            this.RaisePropertyChanged();
        }
    }

    public bool KeyboardJoystickEnabled
    {
        get => _workingConfig.SystemConfig.KeyboardJoystickEnabled;
        set
        {
            if (_workingConfig.SystemConfig.KeyboardJoystickEnabled == value)
                return;
            _workingConfig.SystemConfig.KeyboardJoystickEnabled = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(IsKeyboardJoystickPortEnabled));
        }
    }

    public int KeyboardJoystick
    {
        get => _workingConfig.SystemConfig.KeyboardJoystick;
        set
        {
            if (_workingConfig.SystemConfig.KeyboardJoystick == value)
                return;
            _workingConfig.SystemConfig.KeyboardJoystick = value;
            this.RaisePropertyChanged();
        }
    }

    public bool IsKeyboardJoystickPortEnabled => KeyboardJoystickEnabled;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public Task LoadRomFromDataAsync(string fileName, byte[] data)
    {
        if (data.Length != global::Highbyte.DotNet6502.Systems.Oric.Oric.SystemRomSize)
        {
            StatusMessage = $"{fileName} is {data.Length} bytes; the Atmos ROM must be 16,384 bytes.";
            return Task.CompletedTask;
        }

        _workingConfig.SystemConfig.SetROM(OricSystemConfig.SystemRomName, data: data);
        var rom = _workingConfig.SystemConfig.GetROM(OricSystemConfig.SystemRomName);
        if (!rom.Validate(out var validationErrors, _workingConfig.SystemConfig.EffectiveROMDirectory))
        {
            _workingConfig.SystemConfig.ROMs = [];
            StatusMessage = string.Join(Environment.NewLine, validationErrors);
            RaiseRomProperties();
            return Task.CompletedTask;
        }

        StatusMessage = "Atmos ROM loaded and checksum validated.";
        RaiseRomProperties();
        return Task.CompletedTask;
    }

    private async Task DownloadAsync()
    {
        var acknowledgementHandler = RomLicenseAcknowledgementRequested;
        if (acknowledgementHandler == null)
        {
            StatusMessage = "The ROM licence acknowledgement could not be displayed.";
            return;
        }

        var acknowledgement = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        acknowledgementHandler.Invoke(this, new OricRomLicenseAcknowledgementEventArgs(acknowledgement));
        if (!await acknowledgement.Task)
        {
            StatusMessage = "ROM download cancelled.";
            return;
        }

        try
        {
            SetBusy(true);
            StatusMessage = "Downloading Atmos ROM...";
            var downloader = new RomDownloader(
                _hostApp.LoggerFactory, _httpClient, _hostApp.GetCorsProxyUrl(), _hostApp.GetDownloadCache());

            if (IsRunningInWebAssembly)
            {
                var downloaded = await downloader.DownloadRomsAsync(_workingConfig.SystemConfig.ROMDownloadSources);
                _workingConfig.SystemConfig.SetROM(OricSystemConfig.SystemRomName,
                    data: downloaded[OricSystemConfig.SystemRomName]);
            }
            else
            {
                var written = await downloader.DownloadRomsToFilesAsync(
                    _workingConfig.SystemConfig.ROMDownloadSources,
                    _workingConfig.SystemConfig.EffectiveROMDirectory);
                _workingConfig.SystemConfig.SetROM(OricSystemConfig.SystemRomName,
                    written[OricSystemConfig.SystemRomName]);
            }
            StatusMessage = "Atmos ROM downloaded.";
            RaiseRomProperties();
        }
        catch (Exception ex)
        {
            StatusMessage = $"ROM download failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SaveAsync()
    {
        if (!_workingConfig.IsValid(out var errors))
        {
            StatusMessage = string.Join(Environment.NewLine, errors);
            return;
        }

        _originalConfig.SystemConfig.ROMDirectory = _workingConfig.SystemConfig.ROMDirectory;
        _originalConfig.SystemConfig.ROMs = ROM.Clone(_workingConfig.SystemConfig.ROMs);
        _originalConfig.SystemConfig.CpuCompatibilityProfile = _workingConfig.SystemConfig.CpuCompatibilityProfile;
        _originalConfig.SystemConfig.AudioEnabled = _workingConfig.SystemConfig.AudioEnabled;
        _originalConfig.SystemConfig.JoystickInterface = _workingConfig.SystemConfig.JoystickInterface;
        _originalConfig.SystemConfig.KeyboardJoystickEnabled = _workingConfig.SystemConfig.KeyboardJoystickEnabled;
        _originalConfig.SystemConfig.KeyboardJoystick = _workingConfig.SystemConfig.KeyboardJoystick;
        _originalConfig.InputConfig = (OricInputConfig)_workingConfig.InputConfig.Clone();
        _originalConfig.SystemConfig.SetRenderProviderType(_workingConfig.SystemConfig.RenderProviderType);
        _originalConfig.SystemConfig.SetRenderTargetType(_workingConfig.SystemConfig.RenderTargetType);
        _originalConfig.SystemConfig.SetAudioProviderType(_workingConfig.SystemConfig.AudioProviderType);
        _originalConfig.SystemConfig.SetAudioTargetType(_workingConfig.SystemConfig.AudioTargetType);
        _hostApp.UpdateHostSystemConfig(_originalConfig);
        await _hostApp.PersistCurrentHostSystemConfig();
        ConfigurationChanged?.Invoke(this, true);
    }

    private void SelectAvailableTargets()
    {
        var render = _hostApp.GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations()?.FirstOrDefault();
        if (render.HasValue)
        {
            _workingConfig.SystemConfig.SetRenderProviderType(render.Value.renderProviderType);
            _workingConfig.SystemConfig.SetRenderTargetType(render.Value.renderTargetType);
        }
        var audio = _hostApp.GetAvailableSystemAudioProviderTypesAndAudioTargetTypeCombinations()?.FirstOrDefault();
        if (audio.HasValue)
        {
            _workingConfig.SystemConfig.SetAudioProviderType(audio.Value.audioProviderType);
            _workingConfig.SystemConfig.SetAudioTargetType(audio.Value.audioTargetType);
        }
    }

    private void InitializeJoystickOptions()
    {
        JoystickInterfaces.Add(new(OricJoystickInterface.None, "None"));
        JoystickInterfaces.Add(new(OricJoystickInterface.PASE, "PASE / Altai / Mageco"));
        JoystickInterfaces.Add(new(OricJoystickInterface.IJK, "IJK / Stingy / Egoist"));
        foreach (var joystick in _workingConfig.InputConfig.AvailableJoysticks)
            AvailableJoysticks.Add(joystick);
    }

    private void SetBusy(bool value)
    {
        _isBusy = value;
        this.RaisePropertyChanged(nameof(IsNotBusy));
    }

    private void RaiseRomProperties()
    {
        this.RaisePropertyChanged(nameof(RomLoaded));
        this.RaisePropertyChanged(nameof(RomStatus));
    }
}

public sealed class OricRomLicenseAcknowledgementEventArgs(TaskCompletionSource<bool> completion) : EventArgs
{
    public void SetResult(bool acknowledged) => completion.TrySetResult(acknowledged);
}
