using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

/// <summary>
/// Helper for safely executing async operations in event handlers with WASM-compatible exception handling.
/// On desktop platforms, exceptions propagate normally to global handlers.
/// On WebAssembly, exceptions are manually forwarded to prevent runtime termination.
/// </summary>
public static class SafeAsyncHelper
{
    /// <summary>
    /// Safely executes an async operation.
    /// In WASM, exceptions are forwarded to the global handler to prevent runtime termination.
    /// On desktop, exceptions propagate normally.
    /// </summary>
    public static void Execute(Func<Task> asyncAction)
    {
        try
        {
            Observe(asyncAction());
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    internal static void Observe<T>(IObservable<T> observable)
    {
        try
        {
            observable.Subscribe(NoopObserver<T>.Instance);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private static void Observe(Task task)
    {
        if (task.IsCompleted)
        {
            HandleCompletion(task);
            return;
        }

        _ = task.ContinueWith(
            static completedTask => HandleCompletion(completedTask),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void HandleCompletion(Task task)
    {
        if (task.IsCompletedSuccessfully || task.IsCanceled)
            return;

        if (task.Exception is not { } aggregateException)
            return;

        HandleException(aggregateException.GetBaseException());
    }

    private static void HandleException(Exception exception)
    {
        if (PlatformDetection.IsRunningInWebAssembly())
        {
            App.WasmExceptionHandler?.Invoke(exception);
            return;
        }

        Dispatcher.UIThread.Post(() => ExceptionDispatchInfo.Capture(exception).Throw());
    }

    private sealed class NoopObserver<T> : IObserver<T>
    {
        internal static readonly NoopObserver<T> Instance = new();

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            HandleException(error);
        }

        public void OnNext(T value)
        {
        }
    }
}
