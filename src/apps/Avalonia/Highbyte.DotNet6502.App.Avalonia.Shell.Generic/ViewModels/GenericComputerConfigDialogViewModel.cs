using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Impl.Avalonia.Generic;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Generic.ViewModels;

/// <summary>
/// Generic computer configuration dialog: CPU model and CPU compatibility profile. The
/// Generic machine has no fixed identity, so every CPU model is selectable; everything
/// else about the machine (memory layout, screen, example programs) stays JSON-config
/// only.
/// </summary>
public class GenericComputerConfigDialogViewModel : ViewModelBase
{
    private readonly AvaloniaHostApp _hostApp;
    private readonly GenericComputerHostConfig _originalConfig;
    private readonly GenericComputerHostConfig _workingConfig;

    private bool _isBusy;
    private string? _statusMessage;
    private string? _validationMessage;
    private CpuModelOption? _selectedCpuModel;
    private CpuCompatibilityProfileOption? _selectedCpuCompatibilityProfile;

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public GenericComputerConfigDialogViewModel(AvaloniaHostApp hostApp)
    {
        _hostApp = hostApp ?? throw new ArgumentNullException(nameof(hostApp));
        _originalConfig = hostApp.CurrentHostSystemConfig as GenericComputerHostConfig
            ?? throw new Exception("hostApp.CurrentHostSystemConfig must be type GenericComputerHostConfig");
        _workingConfig = (GenericComputerHostConfig)_originalConfig.Clone();

        // Model first: it constrains the profile list the profile selection lands in.
        SelectedCpuModel = CpuModelOption.FromModelId(_workingConfig.SystemConfig.CpuModelId);
        SelectedCpuCompatibilityProfile = CpuCompatibilityProfileOption.FromProfile(_workingConfig.SystemConfig.CpuCompatibilityProfile);

        SaveCommand = ReactiveCommandHelper.CreateSafeCommand(
            async () =>
            {
                if (await TryApplyChangesAsync())
                    ConfigurationChanged?.Invoke(this, true);
            },
            outputScheduler: RxSchedulers.MainThreadScheduler);

        CancelCommand = ReactiveCommandHelper.CreateSafeCommand(
            () =>
            {
                ConfigurationChanged?.Invoke(this, false);
                return Task.CompletedTask;
            },
            outputScheduler: RxSchedulers.MainThreadScheduler);
    }

    public event EventHandler<bool>? ConfigurationChanged;

    // The Generic machine has no fixed CPU identity — offer every known model.
    public ObservableCollection<CpuModelOption> CpuModels { get; } = new(CpuModelOption.All);
    // Repopulated per selected CPU model (e.g. the 65C02 supports only OfficialOnly).
    public ObservableCollection<CpuCompatibilityProfileOption> CpuCompatibilityProfiles { get; } =
        new(CpuCompatibilityProfileOption.All);

    public bool IsRunningInWebAssembly { get; } = PlatformDetection.IsRunningInWebAssembly();

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
                return;

            this.RaiseAndSetIfChanged(ref _isBusy, value);
            this.RaisePropertyChanged(nameof(IsNotBusy));
            this.RaisePropertyChanged(nameof(CanSave));
        }
    }

    public bool IsNotBusy => !IsBusy;

    public bool CanSave => IsNotBusy;

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
                return;

            this.RaiseAndSetIfChanged(ref _statusMessage, value);
            this.RaisePropertyChanged(nameof(HasStatusMessage));
        }
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (_validationMessage == value)
                return;

            this.RaiseAndSetIfChanged(ref _validationMessage, value);
            this.RaisePropertyChanged(nameof(HasValidationMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);
    public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);

    public string OkButtonText => IsRunningInWebAssembly ? "Save" : "Ok";

    public CpuModelOption? SelectedCpuModel
    {
        get => _selectedCpuModel;
        set
        {
            if (ReferenceEquals(_selectedCpuModel, value))
                return;

            this.RaiseAndSetIfChanged(ref _selectedCpuModel, value);

            if (value != null)
            {
                _workingConfig.SystemConfig.CpuModelId = value.ModelId;
                UpdateCompatibilityProfilesForModel(value.ModelId);
            }

            this.RaisePropertyChanged(nameof(SelectedCpuModelHelpText));
        }
    }

    public string SelectedCpuModelHelpText => SelectedCpuModel?.HelpText ?? string.Empty;

    public CpuCompatibilityProfileOption? SelectedCpuCompatibilityProfile
    {
        get => _selectedCpuCompatibilityProfile;
        set
        {
            if (ReferenceEquals(_selectedCpuCompatibilityProfile, value))
                return;

            this.RaiseAndSetIfChanged(ref _selectedCpuCompatibilityProfile, value);

            if (value != null)
                _workingConfig.SystemConfig.CpuCompatibilityProfile = value.Profile;

            this.RaisePropertyChanged(nameof(SelectedCpuCompatibilityProfileHelpText));
        }
    }

    public string SelectedCpuCompatibilityProfileHelpText => SelectedCpuCompatibilityProfile?.HelpText ?? string.Empty;

    /// <summary>
    /// Constrains the profile dropdown to the profiles the selected model supports, and
    /// auto-corrects the selection when the current profile is no longer among them
    /// (e.g. picking the 65C02 forces "Official only") — the UI can never produce a
    /// model/profile pairing that config validation would reject.
    /// </summary>
    private void UpdateCompatibilityProfilesForModel(string cpuModelId)
    {
        var supportedProfiles = CpuModelInfo.GetSupportedProfiles(cpuModelId);

        CpuCompatibilityProfiles.Clear();
        foreach (var option in CpuCompatibilityProfileOption.All.Where(o => supportedProfiles.Contains(o.Profile)))
            CpuCompatibilityProfiles.Add(option);

        var currentProfile = _workingConfig.SystemConfig.CpuCompatibilityProfile;
        SelectedCpuCompatibilityProfile = CpuCompatibilityProfiles.FirstOrDefault(o => o.Profile == currentProfile)
            ?? CpuCompatibilityProfiles.First();
    }

    public async Task<bool> TryApplyChangesAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Saving...";
            ValidationMessage = string.Empty;

            if (!_workingConfig.IsValid(out var validationErrors))
            {
                StatusMessage = null;
                ValidationMessage = string.Join(Environment.NewLine, validationErrors);
                return false;
            }

            _originalConfig.SystemConfig.CpuModelId = _workingConfig.SystemConfig.CpuModelId;
            _originalConfig.SystemConfig.CpuCompatibilityProfile = _workingConfig.SystemConfig.CpuCompatibilityProfile;

            _hostApp.UpdateHostSystemConfig(_originalConfig);
            await _hostApp.PersistCurrentHostSystemConfig();

            StatusMessage = "Configuration saved.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving config: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
