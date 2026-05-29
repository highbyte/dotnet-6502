using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Input;
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Highbyte.DotNet6502.Systems.Commodore64.Utils.BasicAssistant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64;

/// <summary>
/// C64 system configurer for the SadConsole + NAudio host. Everything system-agnostic comes from
/// <see cref="C64SystemConfigurerCore"/>; this adds the BASIC AI coding assistant + input handler
/// and a couple of host-config tweaks. See <c>docs/system-configurer-consolidation.md</c>.
/// </summary>
public class C64Setup : C64SystemConfigurerCore
{
    public List<string> ConfigurationVariants => C64ModelInventory.C64Models.Keys.ToList();

    public C64Setup(ILoggerFactory loggerFactory, IConfiguration configuration)
        : base(loggerFactory, configuration, () => new C64HostConfig(), C64HostConfig.ConfigSectionName)
    {
    }

    public override async Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        var c64HostConfig = (C64HostConfig)await base.GetNewHostSystemConfig();

        // TODO: Why is the list of ROMs duplicated when binding from appsettings.json?
        //       This is a workaround to remove duplicates.
        c64HostConfig.SystemConfig.ROMs = c64HostConfig.SystemConfig.ROMs.DistinctBy(p => p.Name).ToList();

        // TODO: Code-suggestion AI backend type should not be in system-specific config.
        //       For now workaround by reading from a common setting.
        c64HostConfig.CodeSuggestionBackendType =
            Enum.Parse<CodeSuggestionBackendTypeEnum>(Configuration["CodingAssistant:CodingAssistantType"] ?? "None");

        return c64HostConfig;
    }

    public override Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
    {
        var c64HostConfig = (C64HostConfig)hostSystemConfig;
        var c64 = (C64)system;

        ICodeSuggestion codeSuggestion = CodeSuggestionConfigurator.CreateCodeSuggestion(
            c64HostConfig.CodeSuggestionBackendType, Configuration, LoggerFactory,
            C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION,
            C64BasicCodingAssistant.CODE_COMPLETION_ADDITIONAL_SYSTEM_INSTRUCTION,
            defaultToNoneIdConfigError: true);
        var c64BasicCodingAssistant = new C64BasicCodingAssistant(c64, codeSuggestion, LoggerFactory);
        c64.InputConsumer = new C64InputHandler(c64, LoggerFactory, new C64InputConfig(),
            c64BasicCodingAssistant, c64HostConfig.BasicAIAssistantDefaultEnabled);

        return base.BuildSystemRunner(system, hostSystemConfig);
    }
}
