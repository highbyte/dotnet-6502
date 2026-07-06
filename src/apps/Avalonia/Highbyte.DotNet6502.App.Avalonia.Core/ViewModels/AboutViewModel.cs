using System;
using System.Reflection;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Highbyte.DotNet6502.App.Avalonia.Core.Services;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

/// <summary>
/// Backs the About dialog: always shows the current version, and for a package-manager install adds
/// the update status, the exact <c>brew</c>/<c>scoop</c> command to copy, and a "What's new" link.
/// </summary>
public class AboutViewModel : ViewModelBase
{
    private const string RepositoryUrl = "https://github.com/highbyte/dotnet-6502";

    private readonly IAppUpdateService _updateService;

    private string _statusText = string.Empty;
    private bool _isChecking;
    private bool _isUpdateAvailable;
    private string? _latestVersionDisplay;
    private string? _suggestedCommand;
    private string? _releaseNotesUrl;

    /// <summary>Raised when the dialog should close (host removes the overlay).</summary>
    public event EventHandler? RequestClose;

    /// <summary>Raised after the one-click self-update helper was spawned — the host must now quit the app.</summary>
    public event EventHandler? UpdateStarted;

    public AboutViewModel(IAppUpdateService updateService)
    {
        _updateService = updateService;
        CurrentVersionDisplay = updateService.CurrentVersionDisplay;
        IsUpdateCheckSupported = updateService.IsSupported;
        CurrentReleaseNotesUrl = BuildCurrentReleaseNotesUrl(CurrentVersionDisplay);

        UpdateNowCommand = ReactiveCommandHelper.CreateSafeCommand(UpdateNowAsync);
        CloseCommand = ReactiveCommandHelper.CreateSafeCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));

        StatusText = IsUpdateSectionVisible ? "Checking for updates…" : string.Empty;

        // Opening the About dialog is an explicit user action, so always run a fresh check (force:true
        // bypasses the daily cadence and the disabled-setting/CI gating). No separate "Check now" needed.
        if (IsUpdateCheckSupported)
            _ = CheckAsync(force: true);
    }

    public string CurrentVersionDisplay { get; }
    public bool IsUpdateCheckSupported { get; }
    public bool IsUpdateSectionVisible => IsUpdateCheckSupported;
    public string? CurrentReleaseNotesUrl { get; }
    public bool HasCurrentReleaseNotes => !string.IsNullOrEmpty(CurrentReleaseNotesUrl);
    public string Author => "Highbyte";
    public string RepoUrl => RepositoryUrl;
    public string OSVersion => RuntimeInformation.OSDescription;
    public string DotNetVersion => RuntimeInformation.FrameworkDescription;
    public string AvaloniaVersion => ReadAvaloniaVersion();

    public ReactiveCommand<Unit, Unit> UpdateNowCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    /// <summary>True when a one-click update can be started (managed install with an available update).</summary>
    public bool CanUpdateNow => IsUpdateCheckSupported && _isUpdateAvailable;
    public bool ShowUpdateAvailableStatus => IsUpdateSectionVisible && _isUpdateAvailable;
    public bool ShowPlainStatus => IsUpdateSectionVisible && !_isUpdateAvailable;
    public bool ShowInlineReleaseNotes => ShowUpdateAvailableStatus && HasReleaseNotes;

    public string StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public bool IsChecking
    {
        get => _isChecking;
        private set => this.RaiseAndSetIfChanged(ref _isChecking, value);
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isUpdateAvailable, value);
            this.RaisePropertyChanged(nameof(CanUpdateNow));
            this.RaisePropertyChanged(nameof(ShowUpdateAvailableStatus));
            this.RaisePropertyChanged(nameof(ShowPlainStatus));
            this.RaisePropertyChanged(nameof(ShowInlineReleaseNotes));
        }
    }

    public string? LatestVersionDisplay
    {
        get => _latestVersionDisplay;
        private set
        {
            this.RaiseAndSetIfChanged(ref _latestVersionDisplay, value);
            this.RaisePropertyChanged(nameof(ShowUpdateAvailableStatus));
        }
    }

    /// <summary>The <c>brew</c>/<c>scoop</c> command to run; non-null only when an update is available.</summary>
    public string? SuggestedCommand
    {
        get => _suggestedCommand;
        private set
        {
            this.RaiseAndSetIfChanged(ref _suggestedCommand, value);
            this.RaisePropertyChanged(nameof(HasSuggestedCommand));
        }
    }

    public bool HasSuggestedCommand => !string.IsNullOrEmpty(_suggestedCommand);

    public string? ReleaseNotesUrl
    {
        get => _releaseNotesUrl;
        private set
        {
            this.RaiseAndSetIfChanged(ref _releaseNotesUrl, value);
            this.RaisePropertyChanged(nameof(HasReleaseNotes));
            this.RaisePropertyChanged(nameof(ShowInlineReleaseNotes));
        }
    }

    public bool HasReleaseNotes => !string.IsNullOrEmpty(_releaseNotesUrl);

    private async Task UpdateNowAsync()
    {
        StatusText = "Starting update...";
        var started = await _updateService.TryStartSelfUpdateAsync();
        if (started)
            UpdateStarted?.Invoke(this, EventArgs.Empty); // host quits the app so the upgrade can proceed
        else
            StatusText = "Could not start the automatic update. Copy the command above and run it manually.";
    }

    private async Task CheckAsync(bool force)
    {
        if (!IsUpdateCheckSupported || IsChecking)
            return;

        IsChecking = true;
        StatusText = "Checking for updates…";
        try
        {
            var status = await _updateService.CheckAsync(force);
            ApplyStatus(status);
        }
        catch (Exception ex)
        {
            IsUpdateAvailable = false;
            SuggestedCommand = null;
            ReleaseNotesUrl = null;
            StatusText = $"Update check failed: {ex.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }

    private void ApplyStatus(AppUpdateStatus? status)
    {
        if (status is null)
        {
            StatusText = "Update check did not return any result.";
            IsUpdateAvailable = false;
            SuggestedCommand = null;
            ReleaseNotesUrl = null;
            return;
        }

        IsUpdateAvailable = status.IsUpdateAvailable;
        SuggestedCommand = status.SuggestedCommand;
        ReleaseNotesUrl = status.ReleaseNotesUrl;
        LatestVersionDisplay = status.LatestVersionDisplay;

        if (!status.IsManaged)
            StatusText = "This build isn't installed via Homebrew or Scoop, so there's no update to check for.";
        else if (status.IsUpdateAvailable)
            StatusText = $"Update available: {status.CurrentVersionDisplay} → {status.LatestVersionDisplay}";
        else
            StatusText = $"You're on the latest version ({status.CurrentVersionDisplay}).";
    }

    private static string? BuildCurrentReleaseNotesUrl(string currentVersionDisplay)
    {
        if (!currentVersionDisplay.StartsWith("v", StringComparison.Ordinal))
            return null;

        return $"{RepositoryUrl}/releases/tag/{currentVersionDisplay}";
    }

    private static string ReadAvaloniaVersion()
    {
        try
        {
            var asm = typeof(global::Avalonia.Controls.Control).Assembly;
            var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return infoAttr?.InformationalVersion ?? asm.GetName().Version?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}
