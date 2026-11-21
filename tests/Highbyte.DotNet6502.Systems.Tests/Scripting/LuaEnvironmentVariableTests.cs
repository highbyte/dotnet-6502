using Highbyte.DotNet6502.Scripting.MoonSharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Scripting;

public class LuaEnvironmentVariableTests
{
    [Theory]
    [InlineData("headless")]
    [InlineData("desktop")]
    [InlineData("browser")]
    public void OnlyGrantedNamesCanBeReadOnNativeHosts(string host)
    {
        var name = $"DOTNET6502_TEST_{Guid.NewGuid():N}";
        var emptyName = name + "_EMPTY";
        try
        {
            Environment.SetEnvironmentVariable(name, "value=with spaces");
            Environment.SetEnvironmentVariable(emptyName, "");

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ScriptingConfig.ConfigSectionName}:AllowedEnvironmentVariables:0"] = name,
                [$"{ScriptingConfig.ConfigSectionName}:AllowedEnvironmentVariables:1"] = name + "*",
            }).Build();
            var config = MoonSharpScriptingConfigurator.CreateEffectiveConfig(configuration, NullLoggerFactory.Instance);
            config.AllowedEnvironmentVariables.Add(emptyName);
            config.AllowedEnvironmentVariables.Add(name + "_UNSET");
            RunScript(host, config, $$"""
                local value = emu.getenv("{{name}}")
                local empty = emu.getenv("{{emptyName}}")
                if emu.host() == "browser" then
                    assert(value == nil and empty == nil)
                else
                    assert(value == "value=with spaces" and empty == "")
                end
                assert(emu.getenv("{{name.ToLowerInvariant()}}") == nil)
                assert(emu.getenv("{{name}}_UNSET") == nil)
                """);

            var denied = new ScriptingConfig();
            RunScript(host, denied, $"assert(emu.getenv(\"{name}\") == nil)");
            denied.AllowedEnvironmentVariables.Add("DOTNET6502_TEST_*");
            RunScript(host, denied, $"assert(emu.getenv(\"{name}\") == nil)");
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
            Environment.SetEnvironmentVariable(emptyName, null);
        }
    }

    [Theory]
    [InlineData("headless")]
    [InlineData("browser")]
    public void InvalidNamesRaiseLuaErrorsEvenWithoutGrants(string host)
    {
        RunScript(host, new ScriptingConfig(), """
            assert(not pcall(emu.getenv))
            for _, name in ipairs({false, 1, {}, "", "A=B", "A" .. string.char(0) .. "B"}) do
                assert(not pcall(emu.getenv, name))
            end
            """);
    }

    private static void RunScript(string host, ScriptingConfig config, string source)
    {
        config.AllowFileIO = false;
        config.AllowHttpRequests = false;
        config.AllowStore = false;
        var adapter = new MoonSharpScriptingEngineAdapter(NullLoggerFactory.Instance, host);
        adapter.InitializeVm(null, (_, _) => { }, config, NullLogger.Instance, () => 0, () => 0);
        var handle = adapter.LoadScript(source, "getenv.lua");
        Assert.NotNull(handle);
        Assert.Equal(AdapterCoroutineState.Dead, adapter.InitialResume(handle).CoroutineState);
    }
}
