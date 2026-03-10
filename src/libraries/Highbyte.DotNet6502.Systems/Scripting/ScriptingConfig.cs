namespace Highbyte.DotNet6502.Systems;

public class ScriptingConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.Scripting";

    /// <summary>
    /// Whether Lua scripting is enabled. Default is false.
    /// </summary>
    public bool Enabled { get; set; } = true;

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

    /// <summary>
    /// Whether the <c>file</c> global is available to Lua scripts at all.
    /// When false (default), the <c>file</c> global is not registered — scripts cannot perform any file I/O.
    /// This setting is ignored when running in browser as no file system exists there.
    /// For simple key/value storage that works in both browser and desktop environments, consider using <see cref="AllowStore"/> instead.
    /// </summary>
    public bool AllowFileIO { get; set; } = true;

    /// <summary>
    /// Whether Lua scripts may write, append, or delete files via the <c>file</c> global.
    /// Read operations (<c>file.read</c>, <c>file.read_bytes</c>, <c>file.exists</c>, <c>file.list</c>) are always permitted.
    /// Only relevant when <see cref="AllowFileIO"/> is true. Default: false.
    /// </summary>
    public bool AllowFileWrite { get; set; } = false;

    /// <summary>
    /// Base directory for file I/O operations accessible from Lua scripts.
    /// All paths passed to file operations are resolved relative to this directory;
    /// traversal outside it (e.g. "../") is blocked.
    /// When null or empty, defaults to <see cref="ScriptDirectory"/>.
    /// </summary>
    public string? FileBaseDirectory { get; set; } = null;

    /// <summary>
    /// Whether the <c>http</c> global is available to Lua scripts.
    /// When true, scripts may make outbound HTTP requests (GET, POST) to arbitrary URLs.
    /// Default is false. Not applicable in WASM/browser environments (scripting disabled there).
    /// </summary>
    public bool AllowHttpRequests { get; set; } = true;

    /// <summary>
    /// Optional callback supplying (fileName, content) pairs directly, bypassing filesystem scanning.
    /// When non-null, <see cref="ScriptingEngine"/> calls this instead of reading from <see cref="ScriptDirectory"/>.
    /// Use in environments without filesystem access (e.g. WASM/browser — scripts from localStorage).
    /// Not serialized from configuration; set programmatically before constructing the engine.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Func<IEnumerable<(string fileName, string content)>>? ScriptLoader { get; set; }

    /// <summary>
    /// Whether the <c>store</c> global is available to Lua scripts.
    /// On desktop, the store is backed by files in <see cref="StoreSubDirectory"/> inside <see cref="ScriptDirectory"/>.
    /// In browser, set <see cref="StoreBackend"/> to a localStorage-backed implementation.
    /// Default: false.
    /// </summary>
    public bool AllowStore { get; set; } = true;

    /// <summary>
    /// Subdirectory name within <see cref="ScriptDirectory"/> used for the filesystem store backend.
    /// Default: ".store".
    /// </summary>
    public string StoreSubDirectory { get; set; } = ".store";

    /// <summary>
    /// Custom store backend. When non-null, <see cref="AllowStore"/> will use this backend instead of
    /// the default filesystem backend. Use in environments without filesystem access (e.g. WASM/browser).
    /// Not serialized from configuration; set programmatically before constructing the engine.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IScriptStore? StoreBackend { get; set; }
}
