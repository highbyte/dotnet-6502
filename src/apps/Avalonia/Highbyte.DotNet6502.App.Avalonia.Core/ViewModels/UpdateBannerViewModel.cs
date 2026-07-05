using System;
using System.Reactive;
using System.Threading.Tasks;
using Highbyte.DotNet6502.App.Avalonia.Core.Services;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

/// <summary>
/// Drives the dismissible "update available" banner shown at the top of the desktop window. Stays
/// hidden unless a managed install has a newer release; only rendered by the desktop host (the browser
/// host has no <see cref="IAppUpdateService.IsSupported"/>).
/// </summary>
public class UpdateBannerViewModel : ViewModelBase
{
    private readonly IAppUpdateService _updateService;
    private bool _dismissed;
    private bool _isVisible;
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
            var status = await _updateService.CheckAsync(force: false);
            // Skip if the user already dismissed this exact version on a previous launch.
            if (status is { IsUpdateAvailable: true, IsDismissed: false } && !_dismissed)
            {
                _latestVersionDisplay = status.LatestVersionDisplay;
                Message = $"Update available: {status.CurrentVersionDisplay} → {status.LatestVersionDisplay}";
                IsVisible = true;
            }
        }
        catch
        {
            // A failed startup check must never disrupt the app; the banner just stays hidden.
        }
    }
}
