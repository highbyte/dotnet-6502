using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Highbyte.DotNet6502.App.Avalonia.Core.Services;

namespace Highbyte.DotNet6502.App.Avalonia.Browser;

[SupportedOSPlatform("browser")]
internal sealed partial class BrowserNativeKeyboardInputBridge : INativeKeyboardInputBridge
{
    public bool Start(Action<string> onText, Action onFocusLost) => StartInput(onText, onFocusLost);
    public void Stop() => StopInput();
    public void ObserveAvailability(Action<bool> onChanged) => ObserveTouchInput(onChanged);

    [JSImport("globalThis.dotnet6502NativeKeyboard.observeAvailability")]
    private static partial void ObserveTouchInput(
        [JSMarshalAs<JSType.Function<JSType.Boolean>>] Action<bool> onChanged);

    [JSImport("globalThis.dotnet6502NativeKeyboard.start")]
    private static partial bool StartInput(
        [JSMarshalAs<JSType.Function<JSType.String>>] Action<string> onText,
        [JSMarshalAs<JSType.Function>] Action onFocusLost);

    [JSImport("globalThis.dotnet6502NativeKeyboard.stop")]
    private static partial void StopInput();
}
