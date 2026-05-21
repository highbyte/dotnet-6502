namespace Highbyte.DotNet6502.App.WASM;

/// <summary>
/// Carries a fatal error from <c>Program.cs</c> bootstrap (plug-in discovery / DI registration)
/// to the <c>Index</c> component.
/// <para>
/// Blazor WebAssembly has no way to show a custom UI before the app has mounted — so when
/// bootstrap fails, <c>Program.cs</c> records the error here and still calls <c>RunAsync()</c>,
/// and the <c>Index</c> component renders this message instead of the emulator UI. If the app
/// cannot mount at all, Blazor's own built-in error UI is the last-resort fallback.
/// </para>
/// </summary>
internal static class BootstrapError
{
    /// <summary>The raw error message, or <c>null</c> if bootstrap succeeded.</summary>
    public static string? Message { get; set; }
}
