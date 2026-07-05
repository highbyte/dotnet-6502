using System;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Media;
using Highbyte.DotNet6502.App.Avalonia.Core.Services;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

/// <summary>
/// Drives the dismissible top-of-window banner: normally the green "update available" notice, but on
/// the launch after a one-click update that didn't take effect, an amber "update didn't complete"
/// notice. Only rendered by the desktop host (the browser host has no
/// <see cref="IAppUpdateService.IsSupported"/>).
/// </summary>
public class UpdateBannerViewModel : ViewModelBase
{
    // Update-notice green (Material Green 800), kept in sync with the browser banner; amber for the
    // "update didn't complete" warning state.
    private static readonly IBrush UpdateAvailableBrush = new SolidColorBrush(Color.Parse("#2E7D32"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#B45309"));

    private readonly IAppUpdateService _updateService;
    private bool _dismissed;
    private bool _isVisible;
    private bool _isWarning;
    private string _message = string.Empty;
    private string? _latestVersionDisplay;

    /// <summary>Raised when the user clicks "Details" — the host opens the About dialog in response.</summary>
    public event EventHandler? DetailsRequested;

    public UpdateBannerViewModel(IAppUpdateService updateService)
    {
        _updateService = updateService;
        ShowDetailsCommand = ReactiveCommandHelper.CreateSafeCommand(() => DetailsRequested?.Invoke(this, EventArgs.Empty));
        DismissCommand = ReactiveCommandHelper.CreateSafeCommand(() =>
        {
            _dismissed = true;
            IsVisible = false;
            // Remember this version so the banner won't reappear for it on future launches
            // (a newer version still will).
            if (_latestVersionDisplay != null)
                _updateService.DismissVersion(_latestVersionDisplay);
        });
    }

    public bool IsVisible
    {
        get => _isVisible;
        private set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public string Message
    {
        get => _message;
        private set => this.RaiseAndSetIfChanged(ref _message, value);
    }

    /// <summary>True when the banner is the amber "update didn't complete" warning rather than the green notice.</summary>
    public bool IsWarning
    {
        get => _isWarning;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isWarning, value);
            this.RaisePropertyChanged(nameof(Background));
        }
    }

    /// <summary>Banner background brush: green for "update available", amber for the failure warning.</summary>
    public IBrush Background => _isWarning ? WarningBrush : UpdateAvailableBrush;

    public ReactiveCommand<Unit, Unit> ShowDetailsCommand { get; }
    public ReactiveCommand<Unit, Unit> DismissCommand { get; }

    /// <summary>
    /// Runs the (cadence-cached) startup check and shows the banner if a managed update is available.
    /// Called from the UI thread; awaits without <c>ConfigureAwait(false)</c> so property updates stay
    /// on the UI thread. Never throws.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (!_updateService.IsSupported || _dismissed)
            return;

        try
        {
            // First: did a previous one-click update fail to take effect? (amber, one-shot)
            var failedNotice = _updateService.ConsumeFailedUpdateNotice();
            if (failedNotice != null)
            {
                Message = failedNotice;
                IsWarning = true;
                IsVisible = true;
                return;
            }

            var status = await _updateService.CheckAsync(force: false);
            // Skip if the user already dismissed this exact version on a previous launch.
            if (status is { IsUpdateAvailable: true, IsDismissed: false } && !_dismissed)
            {
                _latestVersionDisplay = status.LatestVersionDisplay;
                Message = $"Update available: {status.CurrentVersionDisplay} → {status.LatestVersionDisplay}";
                IsWarning = false;
                IsVisible = true;
            }
        }
        catch
        {
            // A failed startup check must never disrupt the app; the banner just stays hidden.
        }
    }
}
