using System;
using System.Threading.Tasks;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

/// <summary>
/// Helper for safely executing async operations in event handlers with WASM-compatible exception handling.
/// On desktop platforms, exceptions propagate normally to global handlers.
/// On WebAssembly, exceptions are manually forwarded to prevent runtime termination.
/// </summary>
internal static class SafeAsyncHelper
{
    /// <summary>
    /// Safely executes an async operation.
    /// In WASM, exceptions are forwarded to the global handler to prevent runtime termination.
    /// On desktop, exceptions propagate normally.
    /// </summary>
    internal static async void Execute(Func<Task> asyncAction)
    {
        if (PlatformDetection.IsRunningInWebAssembly())
        {
            try
            {
                await asyncAction();
            }
            catch (Exception ex)
            {
                App.WasmExceptionHandler?.Invoke(ex);
            }
        }
        else
        {
            // On desktop, let exceptions propagate to Dispatcher.UIThread.UnhandledException
            await asyncAction();
        }
    }
}
