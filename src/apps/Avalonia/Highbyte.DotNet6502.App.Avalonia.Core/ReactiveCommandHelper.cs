using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

/// <summary>
/// Helper for creating ReactiveCommands with automatic exception handling for WebAssembly.
/// In WASM, ReactiveCommand's internal exception handling uses threading which fails.
/// This wrapper catches exceptions and dispatches them to the UI thread handler.
/// </summary>
public static class ReactiveCommandHelper
{
    /// <summary>
    /// Creates a ReactiveCommand with automatic exception handling for WebAssembly.
    /// </summary>
    public static ReactiveCommand<Unit, Unit> CreateSafeCommand(
        Func<Task> execute,
        ILogger? logger = null,
        IObservable<bool>? canExecute = null,
        IScheduler? outputScheduler = null)
    {
        if (!PlatformDetection.IsRunningInWebAssembly())
        {
            return ReactiveCommand.CreateFromTask(execute, canExecute, outputScheduler);
        }

        return ReactiveCommand.CreateFromTask(async () =>
        {
            try
            {
                await execute();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Exception in command handler");
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => throw ex);
            }
        }, canExecute, outputScheduler);
    }

    /// <summary>
    /// Creates a ReactiveCommand with parameter and automatic exception handling for WebAssembly.
    /// </summary>
    public static ReactiveCommand<TParam, Unit> CreateSafeCommand<TParam>(
        Func<TParam, Task> execute,
        ILogger? logger = null,
        IObservable<bool>? canExecute = null,
        IScheduler? outputScheduler = null)
    {
        if (!PlatformDetection.IsRunningInWebAssembly())
        {
            return ReactiveCommand.CreateFromTask(execute, canExecute, outputScheduler);
        }

        return ReactiveCommand.CreateFromTask<TParam>(async (param) =>
        {
            try
            {
                await execute(param);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Exception in command handler");
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => throw ex);
            }
        }, canExecute, outputScheduler);
    }

    /// <summary>
    /// Creates a ReactiveCommand with result and automatic exception handling for WebAssembly.
    /// </summary>
    public static ReactiveCommand<Unit, TResult> CreateSafeCommandWithResult<TResult>(
        Func<Task<TResult>> execute,
        ILogger? logger = null,
        IObservable<bool>? canExecute = null,
        IScheduler? outputScheduler = null)
    {
        if (!PlatformDetection.IsRunningInWebAssembly())
        {
            return ReactiveCommand.CreateFromTask(execute, canExecute, outputScheduler);
        }

        return ReactiveCommand.CreateFromTask(async () =>
        {
            try
            {
                return await execute();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Exception in command handler");
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => throw ex);
                throw; // Unreachable, but needed for compiler
            }
        }, canExecute, outputScheduler);
    }

    /// <summary>
    /// Creates a ReactiveCommand with parameter and result and automatic exception handling for WebAssembly.
    /// </summary>
    public static ReactiveCommand<TParam, TResult> CreateSafeCommandWithResult<TParam, TResult>(
        Func<TParam, Task<TResult>> execute,
        ILogger? logger = null,
        IObservable<bool>? canExecute = null,
        IScheduler? outputScheduler = null)
    {
        if (!PlatformDetection.IsRunningInWebAssembly())
        {
            return ReactiveCommand.CreateFromTask(execute, canExecute, outputScheduler);
        }

        return ReactiveCommand.CreateFromTask<TParam, TResult>(async (param) =>
        {
            try
            {
                return await execute(param);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Exception in command handler");
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => throw ex);
                throw; // Unreachable, but needed for compiler
            }
        }, canExecute, outputScheduler);
    }
}
