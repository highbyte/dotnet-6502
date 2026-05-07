using Avalonia.Threading;
using Highbyte.DotNet6502.Systems.Timing;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

/// <summary>
/// Avalonia-specific <see cref="FrameTimer"/> that marshals the Elapsed event to the UI thread.
/// All pacing/precision logic lives in <see cref="FrameTimer"/>.
/// </summary>
public sealed class PeriodicAsyncTimer : FrameTimer
{
    public PeriodicAsyncTimer()
        : base(action => Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Render).GetTask())
    {
    }
}
