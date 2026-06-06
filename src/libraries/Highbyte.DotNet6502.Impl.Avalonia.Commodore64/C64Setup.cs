using System;
using System.Text.Json;
using System.Threading.Tasks;
using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Input;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;
using Highbyte.DotNet6502.Impl.Avalonia.Commodore64.Transport;
using Highbyte.DotNet6502.Systems.Commodore64.Transport;
using Highbyte.DotNet6502.Systems.Commodore64.Utils.BasicAssistant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.Avalonia.Commodore64;

/// <summary>
/// C64 system configurer for the Avalonia + NAudio host. Everything system-agnostic comes from
/// <see cref="C64SystemConfigurerCore"/>; this adds the BASIC AI coding assistant + input handler
/// and JSON config persistence via a host-supplied save delegate.
/// See <c>docs/system-configurer-consolidation.md</c>.
/// </summary>
public class C64Setup : C64SystemConfigurerCore
{
    private readonly Func<string, string, string?, Task>? _saveCustomConfigString;

    public C64Setup(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        Func<string, string, string?, Task>? saveCustomConfigString = null)
        : base(loggerFactory, configuration, () => new C64HostConfig(), C64HostConfig.ConfigSectionName)
    {
        _saveCustomConfigString = saveCustomConfigString;
    }

    public override async Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        if (_saveCustomConfigString == null)
        {
            LoggerFactory.CreateLogger(nameof(C64Setup))
                .LogWarning("No method for saving custom config JSON supplied, so not saving C64HostConfig.");
            return;
        }
        var json = JsonSerializer.Serialize(hostSystemConfig, C64HostConfigJsonContext.Default.C64HostConfig);
        await _saveCustomConfigString(C64HostConfig.ConfigSectionName, json, null);
    }

    public override async Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
    {
        var c64HostConfig = (C64HostConfig)hostSystemConfig;
        var c64 = (C64)system;

        var codeSuggestion = CodeSuggestionConfigurator.CreateCodeSuggestion(
            c64HostConfig.CodeSuggestionBackendType, Configuration, LoggerFactory,
            C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION,
            C64BasicCodingAssistant.CODE_COMPLETION_ADDITIONAL_SYSTEM_INSTRUCTION,
            defaultToNoneIdConfigError: true);
        var c64BasicCodingAssistant = new C64BasicCodingAssistant(c64, codeSuggestion, LoggerFactory);
        c64.InputConsumer = new C64InputHandler(c64, LoggerFactory, c64HostConfig.InputConfig,
            c64BasicCodingAssistant, c64HostConfig.BasicAIAssistantDefaultEnabled);

        if (PlatformDetection.IsRunningInWebAssembly() && c64.SwiftLink != null)
        {
            var transport = CreateBrowserSwiftLinkTransport(c64HostConfig);
            c64.SwiftLink.ReceivePacingCycles =
                c64HostConfig.SwiftLinkTransportMode == C64SwiftLinkTransportMode.HayesModem
                && c64.SwiftLink.ReceiveMode == C64SwiftLinkReceiveMode.Compatible
                    ? GetCyclesPer1200BaudCharacter(c64)
                    : 0;
            c64.SwiftLink.Transport = transport;

            if (c64HostConfig.SwiftLinkConnectOnBoot)
                await transport.ConnectAsync();

            return new SystemRunner(c64);
        }

        return await base.BuildSystemRunner(system, hostSystemConfig);
    }

    protected override bool SupportsSwiftLinkTcpTransport => !PlatformDetection.IsRunningInWebAssembly();

    private ISwiftLinkTransport CreateBrowserSwiftLinkTransport(C64HostConfig c64HostConfig)
        => c64HostConfig.SwiftLinkTransportMode switch
        {
            C64SwiftLinkTransportMode.HayesModem => new HayesModemTransport(
                (_, _) => new WebSocketTransport(
                    c64HostConfig.SwiftLinkWebSocketBridgeUrl ?? string.Empty,
                    c64HostConfig.SwiftLinkSharedToken,
                    c64HostConfig.SwiftLinkBridgeTargetId,
                    LoggerFactory.CreateLogger(nameof(WebSocketTransport))),
                LoggerFactory.CreateLogger(nameof(HayesModemTransport))),
            _ => new WebSocketTransport(
                c64HostConfig.SwiftLinkWebSocketBridgeUrl ?? string.Empty,
                c64HostConfig.SwiftLinkSharedToken,
                c64HostConfig.SwiftLinkBridgeTargetId,
                LoggerFactory.CreateLogger(nameof(WebSocketTransport)))
        };

    private static ulong GetCyclesPer1200BaudCharacter(C64 c64)
    {
        const double BitsPerCharacter = 10.0; // 8N1 framing
        const double BaudRate = 1200.0;
        return (ulong)Math.Ceiling(c64.CpuFrequencyHz * (BitsPerCharacter / BaudRate));
    }
}
