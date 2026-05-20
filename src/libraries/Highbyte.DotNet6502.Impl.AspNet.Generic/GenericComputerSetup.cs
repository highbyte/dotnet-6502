using System.Text.Json;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.Generic.Input;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.AspNet.Generic;

/// <summary>
/// Generic-computer system configurer for the WASM (Blazor) host. Everything system-agnostic
/// comes from <see cref="GenericComputerSystemConfigurerCore"/>; this overrides config
/// load/persist to use browser local storage, sources example program bytes over HTTP, and wires
/// the WASM input handler. See <c>docs/system-configurer-consolidation.md</c>.
/// </summary>
public class GenericComputerSetup : GenericComputerSystemConfigurerCore
{
    private readonly BrowserContext _browserContext;
    private readonly ILogger _logger;

    public GenericComputerSetup(BrowserContext browserContext, ILoggerFactory loggerFactory)
        : base(loggerFactory, () => new GenericComputerHostConfig())
    {
        _browserContext = browserContext;
        _logger = loggerFactory.CreateLogger(nameof(GenericComputerSetup));
    }

    public override async Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        var configKey = $"{GenericComputerHostConfig.ConfigSectionName}";
        var genericComputerHostConfigJson = await _browserContext.LocalStorage.GetItemAsStringAsync(configKey);

        GenericComputerHostConfig? genericComputerHostConfig = null;
        if (!string.IsNullOrEmpty(genericComputerHostConfigJson))
        {
            try
            {
                genericComputerHostConfig = JsonSerializer.Deserialize<GenericComputerHostConfig>(genericComputerHostConfigJson)!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to deserialize GenericComputerHostConfig from Local Storage key: '{configKey}'.");
            }
        }

        if (genericComputerHostConfig == null)
        {
            genericComputerHostConfig = new GenericComputerHostConfig();

            genericComputerHostConfig.SystemConfig.ExamplePrograms = new Dictionary<string, string?>
            {
                { "Scroll", "6502binaries/Generic/Assembler/hostinteraction_scroll_text_and_cycle_colors.prg" },
                { "Snake", "6502binaries/Generic/Assembler/snake6502.prg" },
            };
        }

        return genericComputerHostConfig;
    }

    public override async Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        var genericComputerHostConfig = (GenericComputerHostConfig)hostSystemConfig;
        await _browserContext.LocalStorage.SetItemAsStringAsync($"{GenericComputerHostConfig.ConfigSectionName}", JsonSerializer.Serialize(genericComputerHostConfig));
    }

    // Example programs are served as static web assets and fetched over HTTP.
    protected override async Task<byte[]?> LoadExampleProgramBytesAsync(string exampleProgramPath)
        => await _browserContext.HttpClient.GetByteArrayAsync(exampleProgramPath);

    public override Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
    {
        var genericComputer = (GenericComputer)system;

        genericComputer.InputConsumer = new GenericComputerAspNetInputHandler(
            genericComputer, genericComputer.GenericComputerConfig.Memory.Input);

        return Task.FromResult(new SystemRunner(genericComputer));
    }
}
