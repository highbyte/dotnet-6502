using System.Text.Json;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Oric;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.Avalonia.Oric;

public sealed class OricSetup : OricSystemConfigurerCore
{
    private readonly Func<string, string, string?, Task>? _saveCustomConfigString;

    public OricSetup(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        Func<string, string, string?, Task>? saveCustomConfigString = null)
        : base(loggerFactory, configuration, () => new OricHostConfig(), OricHostConfig.ConfigSectionName)
        => _saveCustomConfigString = saveCustomConfigString;

    public override async Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        if (_saveCustomConfigString is null)
        {
            LoggerFactory.CreateLogger(nameof(OricSetup))
                .LogWarning("No method for saving custom config JSON supplied, so Oric config was not saved.");
            return;
        }
        var json = JsonSerializer.Serialize(hostSystemConfig, OricHostConfigJsonContext.Default.OricHostConfig);
        await _saveCustomConfigString(OricHostConfig.ConfigSectionName, json, null);
    }
}
