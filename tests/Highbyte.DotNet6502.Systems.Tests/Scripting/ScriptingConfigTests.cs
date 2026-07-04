using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Systems.Tests.Scripting;

public class ScriptingConfigTests
{
    [Fact]
    public void ResolvedScriptDirectory_WhenBlank_UsesDefaultScriptDirectory()
    {
        var config = new ScriptingConfig
        {
            ScriptDirectory = string.Empty
        };

        Assert.Equal(ScriptingConfig.DefaultScriptDirectory, config.EffectiveScriptDirectory);
        Assert.Equal(ScriptingConfig.DefaultScriptDirectory, config.ResolvedScriptDirectory());
    }

    [Fact]
    public void ResolvedScriptDirectory_WhenSet_UsesOverride()
    {
        const string configuredPath = "%HOME%/CustomScripts";
        var config = new ScriptingConfig
        {
            ScriptDirectory = configuredPath
        };

        Assert.Equal(configuredPath, config.EffectiveScriptDirectory);
        Assert.Equal(PathHelper.ExpandOSEnvironmentVariables(configuredPath), config.ResolvedScriptDirectory());
    }

    [Fact]
    public void ResolvedScriptDirectory_ExpandsHomeVariableBeforeResolvingPath()
    {
        const string configuredPath = "%HOME%/Documents/Highbyte/DotNet6502/scripts";
        var config = new ScriptingConfig
        {
            ScriptDirectory = configuredPath
        };

        var resolvedPath = config.ResolvedScriptDirectory();

        Assert.Equal(PathHelper.ExpandOSEnvironmentVariables(configuredPath), resolvedPath);
        Assert.DoesNotContain("%HOME%", resolvedPath);
        Assert.False(resolvedPath.StartsWith(AppContext.BaseDirectory, StringComparison.Ordinal));
    }

    [Fact]
    public void ResolvedScriptDirectory_ExpandsTildeBeforeResolvingPath()
    {
        const string configuredPath = "~/Documents/Highbyte/DotNet6502/scripts";
        var config = new ScriptingConfig
        {
            ScriptDirectory = configuredPath
        };

        var resolvedPath = config.ResolvedScriptDirectory();

        Assert.Equal(PathHelper.ExpandOSEnvironmentVariables(configuredPath), resolvedPath);
        Assert.DoesNotContain("~", resolvedPath);
        Assert.False(resolvedPath.StartsWith(AppContext.BaseDirectory, StringComparison.Ordinal));
    }

    [Fact]
    public void ResolvedFileBaseDirectory_ExpandsOverridePath()
    {
        const string configuredPath = "%HOME%/Documents/Highbyte/DotNet6502/scripts/files";
        var config = new ScriptingConfig
        {
            ScriptDirectory = "scripts",
            FileBaseDirectory = configuredPath
        };

        Assert.Equal(PathHelper.ExpandOSEnvironmentVariables(configuredPath), config.ResolvedFileBaseDirectory());
    }

    [Fact]
    public void ResolvedFileBaseDirectory_UsesResolvedScriptDirectory_WhenOverrideIsEmpty()
    {
        const string configuredPath = "%HOME%/Documents/Highbyte/DotNet6502/scripts";
        var config = new ScriptingConfig
        {
            ScriptDirectory = configuredPath,
            FileBaseDirectory = string.Empty
        };

        Assert.Equal(config.ResolvedScriptDirectory(), config.ResolvedFileBaseDirectory());
    }
}
