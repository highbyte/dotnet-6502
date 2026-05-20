using System.Reflection;
using System.Text.Json;
using Highbyte.DotNet6502.Impl.Avalonia.Generic.Input;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.Avalonia.Generic;

/// <summary>
/// Generic-computer system configurer for the Avalonia host. Everything system-agnostic comes
/// from <see cref="GenericComputerSystemConfigurerCore"/>; this sources example program bytes
/// from embedded resources, persists config via a host-supplied save delegate, and wires the
/// Avalonia input handler. See <c>docs/system-configurer-consolidation.md</c>.
/// </summary>
public class GenericComputerSetup : GenericComputerSystemConfigurerCore
{
    private readonly Func<string, string, string?, Task>? _saveCustomConfigJson;

    private readonly Assembly _examplesAssembly = Assembly.GetExecutingAssembly();
    private string? ExampleFileAssemblyName => _examplesAssembly.GetName().Name;

    public GenericComputerSetup(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        Func<string, string, string?, Task>? saveCustomConfigJson = null)
        : base(loggerFactory, configuration, () => new GenericComputerHostConfig(), GenericComputerHostConfig.ConfigSectionName)
    {
        _saveCustomConfigJson = saveCustomConfigJson;
    }

    public override async Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        var hostConfig = (GenericComputerHostConfig)await base.GetNewHostSystemConfig();

        // Fall back to the embedded example programs when none are configured.
        if (hostConfig.SystemConfig.ExamplePrograms.Count == 0
            || (hostConfig.SystemConfig.ExamplePrograms.Count == 1 && hostConfig.SystemConfig.ExamplePrograms.Keys.First() == "None"))
        {
            hostConfig.SystemConfig.ExamplePrograms = new Dictionary<string, string?>
            {
                { "Scroll", $"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Assembler.Generic.hostinteraction_scroll_text_and_cycle_colors.prg" },
                { "Snake",  $"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Assembler.Generic.snake6502.prg" }
            };
        }

        return hostConfig;
    }

    public override async Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        if (_saveCustomConfigJson == null)
            return;

        var genericComputerHostConfig = (GenericComputerHostConfig)hostSystemConfig;
        await _saveCustomConfigJson(GenericComputerHostConfig.ConfigSectionName, JsonSerializer.Serialize(genericComputerHostConfig), null);
    }

    protected override Task<byte[]?> LoadExampleProgramBytesAsync(string exampleProgramPath)
    {
        // Configured paths may be bare file names — prepend this assembly's resource namespace prefix.
        if (!exampleProgramPath.StartsWith(ExampleFileAssemblyName!))
            exampleProgramPath = $"{ExampleFileAssemblyName}.Resources.Sample6502Programs.Assembler.Generic.{exampleProgramPath}";

        using var resourceStream = _examplesAssembly.GetManifestResourceStream(exampleProgramPath);
        if (resourceStream == null)
            throw new Exception($"Cannot find file in embedded resources. Resource: {exampleProgramPath}");

        var exampleProgramBytes = new byte[resourceStream.Length];
        resourceStream.ReadExactly(exampleProgramBytes);
        return Task.FromResult<byte[]?>(exampleProgramBytes);
    }

    public override Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
    {
        var genericComputer = (GenericComputer)system;

        genericComputer.InputConsumer = new AvaloniaGenericInputHandler(
            genericComputer, genericComputer.GenericComputerConfig.Memory.Input, LoggerFactory);

        return Task.FromResult(new SystemRunner(genericComputer));
    }
}
