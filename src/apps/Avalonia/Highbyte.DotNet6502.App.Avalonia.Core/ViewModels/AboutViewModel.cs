using System;
using System.Reactive;
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
    private readonly IAppUpdateService _updateService;

    private string _statusText = string.Empty;
    private bool _isChecking;
    private bool _isUpdateAvailable;
    private string? _latestVersionDisplay;
    private string? _suggestedCommand;
    private string? _releaseNotesUrl;

    /// <summary>Raised when the dialog should close (host removes the overlay).</summary>
    public event EventHandler? RequestClose;

    public AboutViewModel(IAppUpdateService updateService)
    {
        _updateService = updateService;
        CurrentVersionDisplay = updateService.CurrentVersionDisplay;
        IsUpdateCheckSupported = updateService.IsSupported;

        CheckNowCommand = ReactiveCommandHelper.CreateSafeCommand(() => CheckAsync(force: true));
        CloseCommand = ReactiveCommandHelper.CreateSafeCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));

        StatusText = IsUpdateCheckSupported
            ? "Checking for updates…"
            : "Update checks aren't available for this build.";

        if (IsUpdateCheckSupported)
            _ = CheckAsync(force: false); // populate from the cadence cache on open
    }

    public string CurrentVersionDisplay { get; }
    public bool IsUpdateCheckSupported { get; }

    public ReactiveCommand<Unit, Unit> CheckNowCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

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
        private set => this.RaiseAndSetIfChanged(ref _isUpdateAvailable, value);
    }

    public string? LatestVersionDisplay
    {
        get => _latestVersionDisplay;
        private set => this.RaiseAndSetIfChanged(ref _latestVersionDisplay, value);
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
        }
    }

    public bool HasReleaseNotes => !string.IsNullOrEmpty(_releaseNotesUrl);

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
            StatusText = "Update checks aren't available for this build.";
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
}
