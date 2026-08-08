using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

/// <summary>
/// Pulses a button's background between its normal colour and an attention colour, to point the user
/// at something they have to deal with before the emulator can run — in practice, a system whose
/// configuration is invalid because its ROM files are missing.
///
/// <para>
/// The smooth fade is not produced here. This only sets <see cref="Button.Background"/> to two
/// values in a loop; the button itself must declare a <c>BrushTransition</c> on <c>Background</c> in
/// XAML, and <see cref="FlashOnDuration"/> is matched to that transition's duration so the colour is
/// never yanked mid-fade.
/// </para>
///
/// <para>
/// One controller instance per view, holding at most one running flash. The awkward parts —
/// capturing the token source into a local before awaiting, and only clearing the field when it is
/// still the one this call created — exist because a restart can race an in-flight loop; see
/// <see cref="StartAsync"/>.
/// </para>
/// </summary>
public sealed class ButtonFlashController
{
    /// <summary>How long the attention colour is held. Matches the button's BrushTransition duration
    /// so the fade completes before the colour is set back.</summary>
    public static readonly TimeSpan FlashOnDuration = TimeSpan.FromMilliseconds(700);

    /// <summary>How long the normal colour is held between pulses.</summary>
    public static readonly TimeSpan FlashOffDuration = TimeSpan.FromMilliseconds(2000);

    private CancellationTokenSource? _cancellation;

    /// <summary>True while a flash loop is running.</summary>
    public bool IsFlashing => _cancellation != null;

    /// <summary>
    /// Starts (or restarts) the flash on <paramref name="button"/>. Fire-and-forget: the loop runs
    /// until <see cref="Cancel"/>, or until the user clicks the button when
    /// <paramref name="stopAfterClick"/> is set — clicking it means they have seen the hint.
    /// </summary>
    public void Start(Button button, Color flashColor, bool stopAfterClick)
        => SafeAsyncHelper.Execute(() => StartAsync(button, flashColor, stopAfterClick));

    /// <summary>
    /// Cancels a running flash without starting a new one, restoring the button's normal colour.
    /// Safe to call when nothing is running.
    /// </summary>
    public void Cancel()
    {
        var cancellation = _cancellation;
        if (cancellation == null)
            return;

        _cancellation = null;
        SafeAsyncHelper.Execute(async () =>
        {
            await cancellation.CancelAsync();
            cancellation.Dispose(); // safe; the loop's finally may dispose it too
        });
    }

    private async Task StartAsync(Button button, Color flashColor, bool stopAfterClick)
    {
        // Capture the field into a local before awaiting, so the finally block's null-clear in
        // another concurrent call cannot turn this into a NullReferenceException on Dispose().
        var existingCancellation = _cancellation;
        if (existingCancellation != null)
        {
            _cancellation = null;
            await existingCancellation.CancelAsync();
            existingCancellation.Dispose();
        }

        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;

        // Captured after any previous flash has been cancelled and had its colour restored above.
        // Capturing while the button is still mid-pulse would record the attention colour as the
        // "normal" one, leaving both halves of the loop the same colour and the flash looking stuck.
        var originalBrush = button.Background;
        var flashBrush = new SolidColorBrush(flashColor);

        EventHandler<RoutedEventArgs>? clickHandler = null;
        clickHandler = (_, _) => SafeAsyncHelper.Execute(async () =>
        {
            await cancellation.CancelAsync();
            button.Click -= clickHandler;
        });
        if (stopAfterClick)
            button.Click += clickHandler;

        try
        {
            while (!cancellation.Token.IsCancellationRequested)
            {
                button.Background = flashBrush;
                await Task.Delay(FlashOnDuration, cancellation.Token);

                button.Background = originalBrush;
                await Task.Delay(FlashOffDuration, cancellation.Token);
            }
        }
        catch (OperationCanceledException)
        {
            button.Background = originalBrush;
        }
        finally
        {
            button.Click -= clickHandler;
            cancellation.Dispose();

            // Only clear the field if it is still ours: a newer Start may already have replaced it.
            if (ReferenceEquals(_cancellation, cancellation))
                _cancellation = null;
        }
    }
}
