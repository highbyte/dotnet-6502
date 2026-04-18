using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;

namespace Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;

/// <summary>
/// Supplies native-menu items and key bindings for the currently selected emulator system.
/// Implementations live on the system's menu ViewModel (e.g. C64MenuViewModel) so that the
/// shortcuts dispatch directly to the ViewModel commands without code-behind glue.
///
/// Platform split:
/// - macOS:         GetNativeMenuItems() is installed as a NativeMenu on the Application.
///                  On macOS, NativeMenu items appear in the OS-level system menu bar (not
///                  inside the app window), which is the desired UX. The menu bar is also
///                  exposed via the macOS Accessibility API (AXMenuItem), making shortcuts
///                  self-describing and discoverable by AI agents at runtime.
/// - Windows/Linux: NativeMenu would render as in-window chrome on these platforms, which
///                  is not desired. GetKeyBindings() is used instead — shortcuts are registered
///                  directly on the main Window and fire regardless of which child has focus,
///                  but they are invisible to accessibility tools and require prior knowledge.
/// - WASM:          Neither applies; this interface is a no-op in the browser target.
/// </summary>
public interface ISystemMenuContributor
{
    string MenuLabel { get; }

    IReadOnlyList<NativeMenuItemBase> GetNativeMenuItems();

    IReadOnlyList<KeyBinding> GetKeyBindings();
}
