using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Highbyte.DotNet6502.Systems.Input;

/// <summary>
/// Detects the host's currently active native keyboard layout as a raw, platform-specific
/// identifier string.
///
/// Used to auto-select a system keyboard mapping (e.g. the C64 keyboard layout) when the user has
/// not pinned one explicitly. The OS/UI culture is unreliable for this — a user can run an
/// English-language OS with a non-US physical keyboard — so this queries the OS for the actual
/// active keyboard layout.
///
/// Returns the raw identifier only; interpreting it (mapping a layout to a specific system
/// mapping) is the caller's job. Detection is best-effort and returns <c>null</c> when the
/// platform is not supported (Linux, browser) or any detection step fails.
///
/// Supported:
/// <list type="bullet">
/// <item>Windows — the active layout's KLID (8 hex digits, e.g. <c>0000041D</c> = Swedish), via
///   <c>GetKeyboardLayoutNameW</c>.</item>
/// <item>macOS — the active keyboard layout input-source id (e.g.
///   <c>com.apple.keylayout.Swedish</c>), via the Text Input Sources API in HIToolbox. That API
///   is current/supported — it is <em>not</em> part of deprecated Carbon — but it is a C API,
///   hence the CoreFoundation interop here.</item>
/// </list>
/// </summary>
public static class KeyboardLayoutDetector
{
    /// <summary>
    /// Returns the raw native keyboard-layout identifier for the current host, or <c>null</c>
    /// when it cannot be determined (unsupported platform, or any detection failure).
    /// </summary>
    public static string? DetectNativeLayoutId()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return DetectWindowsLayout();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return DetectMacOSLayout();
        }
        catch
        {
            // Best-effort: any interop failure is treated as "not detectable".
        }
        return null;
    }

    // ---------------------------------------------------------------- Windows

    [SupportedOSPlatform("windows")]
    private static string? DetectWindowsLayout()
    {
        // KL_NAMELENGTH is 9 (8 hex digits + null terminator).
        var klid = new StringBuilder(16);
        return GetKeyboardLayoutName(klid) && klid.Length > 0 ? klid.ToString() : null;
    }

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetKeyboardLayoutNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardLayoutName(StringBuilder pwszKLID);

    // ------------------------------------------------------------------ macOS

    private const string CarbonFramework = "/System/Library/Frameworks/Carbon.framework/Carbon";
    private const string CoreFoundationFramework = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const uint CFStringEncodingUTF8 = 0x08000100;

    [SupportedOSPlatform("macos")]
    private static string? DetectMacOSLayout()
    {
        // TISCopyCurrentKeyboardLayoutInputSource returns an owned reference (Copy) -> must CFRelease.
        IntPtr inputSource = TISCopyCurrentKeyboardLayoutInputSource();
        if (inputSource == IntPtr.Zero)
            return null;
        try
        {
            IntPtr propertyKey = GetCarbonStringConstant("kTISPropertyInputSourceID");
            if (propertyKey == IntPtr.Zero)
                return null;

            // TISGetInputSourceProperty returns a borrowed reference (Get) -> do NOT release.
            IntPtr cfId = TISGetInputSourceProperty(inputSource, propertyKey);
            return CFStringToString(cfId);
        }
        finally
        {
            CFRelease(inputSource);
        }
    }

    // kTISPropertyInputSourceID is an exported CFStringRef constant in HIToolbox; the exported
    // symbol is the address of the variable holding the CFStringRef, so it must be dereferenced.
    [SupportedOSPlatform("macos")]
    private static IntPtr GetCarbonStringConstant(string symbolName)
    {
        if (!NativeLibrary.TryLoad(CarbonFramework, out IntPtr lib))
            return IntPtr.Zero;
        if (!NativeLibrary.TryGetExport(lib, symbolName, out IntPtr symbolAddress))
            return IntPtr.Zero;
        return Marshal.ReadIntPtr(symbolAddress);
    }

    [SupportedOSPlatform("macos")]
    private static string? CFStringToString(IntPtr cfString)
    {
        if (cfString == IntPtr.Zero)
            return null;
        long length = CFStringGetLength(cfString);
        if (length == 0)
            return string.Empty;
        long maxByteCount = CFStringGetMaximumSizeForEncoding(length, CFStringEncodingUTF8) + 1;
        var buffer = new byte[maxByteCount];
        if (!CFStringGetCString(cfString, buffer, maxByteCount, CFStringEncodingUTF8))
            return null;
        int terminator = Array.IndexOf(buffer, (byte)0);
        return Encoding.UTF8.GetString(buffer, 0, terminator >= 0 ? terminator : buffer.Length);
    }

    [SupportedOSPlatform("macos")]
    [DllImport(CarbonFramework)]
    private static extern IntPtr TISCopyCurrentKeyboardLayoutInputSource();

    [SupportedOSPlatform("macos")]
    [DllImport(CarbonFramework)]
    private static extern IntPtr TISGetInputSourceProperty(IntPtr inputSource, IntPtr propertyKey);

    [SupportedOSPlatform("macos")]
    [DllImport(CoreFoundationFramework)]
    private static extern long CFStringGetLength(IntPtr theString);

    [SupportedOSPlatform("macos")]
    [DllImport(CoreFoundationFramework)]
    private static extern long CFStringGetMaximumSizeForEncoding(long length, uint encoding);

    [SupportedOSPlatform("macos")]
    [DllImport(CoreFoundationFramework)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFStringGetCString(IntPtr theString, byte[] buffer, long bufferSize, uint encoding);

    [SupportedOSPlatform("macos")]
    [DllImport(CoreFoundationFramework)]
    private static extern void CFRelease(IntPtr cf);
}
