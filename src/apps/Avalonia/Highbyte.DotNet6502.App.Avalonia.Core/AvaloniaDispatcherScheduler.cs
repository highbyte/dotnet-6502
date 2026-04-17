using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using Avalonia.Threading;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

// Replacement for Avalonia.ReactiveUI's AvaloniaScheduler.
//
// Avalonia.ReactiveUI was deprecated by the Avalonia team when Avalonia 12 shipped
// ("legacy and no longer maintained", per the NuGet listing) and no 12.x version was
// released. That package's only role in this codebase was the .UseReactiveUI() AppBuilder
// extension, which set RxApp.MainThreadScheduler = AvaloniaScheduler.Instance so that
// ReactiveCommand / WhenAny / ObservableAsPropertyHelper callbacks run on the UI thread.
//
// We keep the ReactiveUI core package (separate, .NET Foundation, actively maintained) and
// register this scheduler instead. See App.OnFrameworkInitializationCompleted.
internal sealed class AvaloniaDispatcherScheduler : LocalScheduler
{
    public static readonly AvaloniaDispatcherScheduler Instance = new();

    private AvaloniaDispatcherScheduler() { }

    public override IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
    {
        var disposable = new SingleAssignmentDisposable();
        var delay = Scheduler.Normalize(dueTime);

        if (delay == TimeSpan.Zero)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!disposable.IsDisposed)
                    disposable.Disposable = action(this, state);
            });
            return disposable;
        }

        var timer = new DispatcherTimer(delay, DispatcherPriority.Background, (_, _) => { });
        timer.Tick += OnTick;
        timer.Start();
        disposable.Disposable = Disposable.Create(() =>
        {
            timer.Tick -= OnTick;
            timer.Stop();
        });
        return disposable;

        void OnTick(object? sender, EventArgs e)
        {
            timer.Tick -= OnTick;
            timer.Stop();
            if (!disposable.IsDisposed)
                disposable.Disposable = action(this, state);
        }
    }
}
