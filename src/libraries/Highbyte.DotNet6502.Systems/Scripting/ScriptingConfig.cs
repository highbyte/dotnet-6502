namespace Highbyte.DotNet6502.Systems;

public class ScriptingConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.Scripting";

    /// <summary>
    /// Whether Lua scripting is enabled. Default is false.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Directory path where .lua script files are loaded from.
    /// Can be absolute or relative to the application working directory.
    /// </summary>
    public string ScriptDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Log a warning if a script hook (on_before_frame / on_after_frame) takes longer than this many milliseconds.
    /// Set to 0 to disable the warning. Default is 5ms.
    /// </summary>
    public int MaxExecutionWarningMs { get; set; } = 5;

    /// <summary>
    /// Maximum number of Lua VM instructions a coroutine may execute per resume before it is
    /// considered a runaway script and forcibly terminated. This prevents scripts that forget to
    /// call <c>emu.yield()</c> or <c>emu.frameadvance()</c> from hanging the emulator.
    /// Set to 0 to disable the limit. Default is 1,000,000.
    /// </summary>
    public int MaxInstructionsPerResume { get; set; } = 1_000_000;

    /// <summary>
    /// Whether individual scripts should start enabled when loaded.
    /// When false (default), all scripts are loaded but user-disabled at startup
    /// and must be enabled manually via the Scripts tab.
    /// </summary>
    public bool EnableScriptsAtStart { get; set; } = false;
}
