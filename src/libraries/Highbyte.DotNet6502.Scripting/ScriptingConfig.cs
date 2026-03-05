namespace Highbyte.DotNet6502.Scripting;

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
}
