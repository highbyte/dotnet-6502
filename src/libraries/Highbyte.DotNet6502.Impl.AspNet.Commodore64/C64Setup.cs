using Highbyte.DotNet6502.Impl.AspNet;
using Microsoft.Extensions.Logging;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Input;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Blazored.LocalStorage;
using Highbyte.DotNet6502.AI.CodingAssistant.Inference.OpenAI;
using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.Systems.Commodore64.Utils.BasicAssistant;
using static Highbyte.DotNet6502.AI.CodingAssistant.CustomAIEndpointCodeSuggestion;
using System.Text.Json;
using Highbyte.DotNet6502.Systems.Commodore64.Audio;
using Highbyte.DotNet6502.Systems.Commodore64.Render.CustomGeneral;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64;

/// <summary>
/// C64 system configurer for the WASM (Blazor) + WebAudio host. Everything system-agnostic comes
/// from <see cref="C64SystemConfigurerCore"/>; this overrides config load/persist to use browser
/// local storage and wires the BASIC AI coding assistant + input handler.
/// See <c>docs/system-configurer-consolidation.md</c>.
/// </summary>
public class C64Setup : C64SystemConfigurerCore
{
    private const string LEGACY_LOCAL_STORAGE_ROM_PREFIX = "rom_";

    private readonly BrowserContext _browserContext;
    private readonly ILogger _logger;
    private const string DEFAULT_CUSTOMENDPOINT = "https://highbyte-dotnet6502-codecompletion.azurewebsites.net/";
    // Note: For now, use a public visible key as default just to prevent at least some random users to access the endpoint...
    private const string DEFAULT_CUSTOMENDPOINT_APIKEY = "9fe8f8161c1d43251a46bb576336a1a25d7ab607cb5a1b4b960c0949d87bced7";

    public C64Setup(BrowserContext browserContext, ILoggerFactory loggerFactory)
        : base(loggerFactory, () => new C64HostConfig())
    {
        _browserContext = browserContext;
        _logger = loggerFactory.CreateLogger(nameof(C64Setup));
    }

    // The WASM host always renders the C64 via the custom render provider.
    protected override Type? DefaultRenderProviderType => typeof(C64CustomRenderProvider);
    protected override bool SupportsSwiftLinkTcpTransport => false;

    public override async Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        var configKey = $"{C64HostConfig.ConfigSectionName}";
        var c64HostConfigJson = await _browserContext.LocalStorage.GetItemAsStringAsync(configKey);

        C64HostConfig? c64HostConfig = null;
        if (!string.IsNullOrEmpty(c64HostConfigJson))
        {
            try
            {
                c64HostConfig = JsonSerializer.Deserialize<C64HostConfig>(c64HostConfigJson)!;

                // Note: Because ROMDirectory should never be used when running WASM, make sure it is empty (regardless if config from local storage has it set).
                c64HostConfig.SystemConfig.ROMDirectory = "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to deserialize C64HostConfig from Local Storage key: '{configKey}'.");
            }
        }

        if (c64HostConfig == null)
        {
            c64HostConfig = new C64HostConfig();
            // Set ROMDirectory to skip loading ROMs from file system (ROMDirectory + File property),
            // instead assume loading ROMs from byte array (Data property).
            c64HostConfig.SystemConfig.ROMDirectory = "";
            // Audio disabled by default until Web audio playback is more stable
            c64HostConfig.SystemConfig.AudioEnabled = false;
            // The Blazor WASM/Skia host only registers WebAudioCommandTarget — no sample target —
            // so override the system-wide default of C64SidSampleProvider with the command stream.
            c64HostConfig.SystemConfig.SetAudioProviderType(typeof(C64SidCommandStream));
        }

        // TODO: After a while, remove this code that tries to load ROMs from old location.
        // - For a while only the C64 ROMs where stored in specific local storage keys (which is now stored as part of one config key with JSON)
        // - Try to load ROMs from the old location.
        if (c64HostConfig.SystemConfig.ROMs.Count == 0)
        {
            c64HostConfig.SystemConfig.ROMs = await GetROMsFromLocalStorageLegacy();
        }

        return c64HostConfig;
    }

    public override async Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        var c64HostConfig = (C64HostConfig)hostSystemConfig;
        await _browserContext.LocalStorage.SetItemAsStringAsync($"{C64HostConfig.ConfigSectionName}", JsonSerializer.Serialize(c64HostConfig));
    }

    public override async Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        IHostSystemConfig hostSystemConfig
        )
    {
        var c64 = (C64)system;
        var c64HostConfig = (C64HostConfig)hostSystemConfig;

        var codeSuggestion = await GetCodeSuggestionImplementation(c64HostConfig, LoggerFactory, _browserContext.LocalStorage);
        var c64BasicCodingAssistant = new C64BasicCodingAssistant(c64, codeSuggestion, LoggerFactory);
        c64.InputConsumer = new C64InputHandler(c64, LoggerFactory, c64HostConfig.InputConfig, c64BasicCodingAssistant, c64HostConfig.BasicAIAssistantDefaultEnabled);

        return await base.BuildSystemRunner(system, hostSystemConfig);
    }

    private async Task<List<ROM>> GetROMsFromLocalStorageLegacy()
    {
        var roms = new List<ROM>();
        string name;
        byte[]? data;

        name = C64SystemConfig.BASIC_ROM_NAME;
        data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LEGACY_LOCAL_STORAGE_ROM_PREFIX}{name}");
        if (data != null)
        {
            roms.Add(new ROM
            {
                Name = name,
                Data = data,
            });
        }
        name = C64SystemConfig.KERNAL_ROM_NAME;
        data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LEGACY_LOCAL_STORAGE_ROM_PREFIX}{name}");
        if (data != null)
        {
            roms.Add(new ROM
            {
                Name = name,
                Data = data,
            });
        }
        name = C64SystemConfig.CHARGEN_ROM_NAME;
        data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LEGACY_LOCAL_STORAGE_ROM_PREFIX}{name}");
        if (data != null)
        {
            roms.Add(new ROM
            {
                Name = name,
                Data = data,
            });
        }

        return roms;
    }

    public async Task<byte[]> GetROMFromUrl(HttpClient httpClient, string url)
    {
        return await httpClient.GetByteArrayAsync(url);
    }

    public static async Task<ICodeSuggestion> GetCodeSuggestionImplementation(C64HostConfig c64HostConfig, ILoggerFactory loggerFactory, ILocalStorageService localStorageService, bool defaultToNoneIdConfigError = true)
    {
        ICodeSuggestion codeSuggestion;
        try
        {
            if (c64HostConfig.CodeSuggestionBackendType == CodeSuggestionBackendTypeEnum.OpenAI)
            {
                var openAIApiConfig = await GetOpenAIConfig(localStorageService);
                codeSuggestion = OpenAICodeSuggestion.CreateOpenAICodeSuggestion(openAIApiConfig, loggerFactory, C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION, C64BasicCodingAssistant.CODE_COMPLETION_ADDITIONAL_SYSTEM_INSTRUCTION);
            }
            else if (c64HostConfig.CodeSuggestionBackendType == CodeSuggestionBackendTypeEnum.OpenAISelfHostedCodeLlama)
            {
                var openAIApiConfig = await GetOpenAISelfHostedCodeLlamaConfig(localStorageService);
                codeSuggestion = OpenAICodeSuggestion.CreateOpenAICodeSuggestionForCodeLlama(openAIApiConfig, loggerFactory, C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION, C64BasicCodingAssistant.CODE_COMPLETION_ADDITIONAL_SYSTEM_INSTRUCTION);
            }
            else if (c64HostConfig.CodeSuggestionBackendType == CodeSuggestionBackendTypeEnum.CustomEndpoint)
            {
                var customAIEndpointConfig = await GetCustomAIEndpointConfig(localStorageService);
                codeSuggestion = new CustomAIEndpointCodeSuggestion(customAIEndpointConfig, loggerFactory, C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION);
            }
            else if (c64HostConfig.CodeSuggestionBackendType == CodeSuggestionBackendTypeEnum.None)
            {
                codeSuggestion = new NoCodeSuggestion();
            }
            else
            {
                throw new NotImplementedException($"CodeSuggestionBackendType '{c64HostConfig.CodeSuggestionBackendType}' is not implemented.");
            }
        }
        catch (Exception)
        {
            if (defaultToNoneIdConfigError)
                codeSuggestion = new NoCodeSuggestion();
            else
                throw;
        }
        return codeSuggestion;

    }

    public static async Task<ApiConfig> GetOpenAIConfig(ILocalStorageService localStorageService)
    {
        var apiKey = await localStorageService.GetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:ApiKey");

        var deploymentName = await localStorageService.GetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:DeploymentName");
        if (string.IsNullOrEmpty(deploymentName))
            deploymentName = "gpt-4o";  // Default to a OpenAI model that works well

        // For future use: Endpoint can be set if OpenAI is accessed via Azure endpoint.
        //var endpoint = await localStorageService.GetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:Endpoint");
        //Uri.TryCreate(endpoint, UriKind.Absolute, out var endPointUri);

        var apiConfig = new ApiConfig()
        {
            ApiKey = apiKey,    // Api key for OpenAI (required), Azure OpenAI (required), or SelfHosted (optional).
            DeploymentName = deploymentName, // AI model name
            //Endpoint = endPointUri,     // Used if using Azure OpenAI
            SelfHosted = false,
        };
        return apiConfig;
    }

    public static async Task<ApiConfig> GetOpenAISelfHostedCodeLlamaConfig(ILocalStorageService localStorageService)
    {
        var apiKey = await localStorageService.GetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION_SELF_HOSTED}:ApiKey");
        if (apiKey == string.Empty)
            apiKey = null;
        var deploymentName = await localStorageService.GetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION_SELF_HOSTED}:DeploymentName");
        if (string.IsNullOrEmpty(deploymentName))
            deploymentName = "codellama:13b-code"; // Default to a Ollama CodeLlama-code model that seems to work OK (but not as good as OpenAI gpt-4o)
        var endpoint = await localStorageService.GetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION_SELF_HOSTED}:Endpoint");
        if (string.IsNullOrEmpty(endpoint))
            endpoint = "http://localhost:11434/api"; // Default to local Ollama
        Uri.TryCreate(endpoint, UriKind.Absolute, out var endPointUri);

        var apiConfig = new ApiConfig()
        {
            ApiKey = apiKey,    // Optional for Self-hosted model.
            DeploymentName = deploymentName, // AI CodeLlama-code model name (ex: codellama:13b-code, codellama:7b-code)
            Endpoint = endPointUri,     // Self-hosted OpenAI API compatible endpoint (for example Ollama)
            SelfHosted = true // Set to true to use self-hosted OpenAI API compatible endpoint.
        };
        return apiConfig;
    }

    public static async Task<CustomAIEndpointConfig> GetCustomAIEndpointConfig(ILocalStorageService localStorageService)
    {
        var apiKey = await localStorageService.GetItemAsStringAsync($"{CustomAIEndpointConfig.CONFIG_SECTION}:ApiKey");
        if (string.IsNullOrEmpty(apiKey))
            apiKey = DEFAULT_CUSTOMENDPOINT_APIKEY;

        var endpoint = await localStorageService.GetItemAsStringAsync($"{CustomAIEndpointConfig.CONFIG_SECTION}:Endpoint");
        if (string.IsNullOrEmpty(endpoint))
            endpoint = DEFAULT_CUSTOMENDPOINT;
        Uri.TryCreate(endpoint, UriKind.Absolute, out var endPointUri);

        var apiConfig = new CustomAIEndpointConfig()
        {
            ApiKey = apiKey,
            Endpoint = endPointUri,
        };
        return apiConfig;
    }

    public static async Task SaveOpenAICodingAssistantConfigToLocalStorage(ILocalStorageService localStorageService, ApiConfig apiConfig)
    {
        await localStorageService.SetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:ApiKey", apiConfig.ApiKey ?? string.Empty);
        //await localStorageService.SetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:Endpoint", apiConfig.Endpoint != null ? apiConfig.Endpoint.OriginalString : string.Empty);
    }

    public static async Task SaveOpenAISelfHostedCodeLlamaCodingAssistantConfigToLocalStorage(ILocalStorageService localStorageService, ApiConfig apiConfig)
    {
        await localStorageService.SetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION_SELF_HOSTED}:ApiKey", apiConfig.ApiKey ?? string.Empty);
        await localStorageService.SetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION_SELF_HOSTED}:DeploymentName", apiConfig.DeploymentName ?? string.Empty);
        await localStorageService.SetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION_SELF_HOSTED}:Endpoint", apiConfig.Endpoint != null ? apiConfig.Endpoint.OriginalString : string.Empty);
    }

    public static async Task SaveCustomCodingAssistantConfigToLocalStorage(ILocalStorageService localStorageService, CustomAIEndpointConfig customAIEndpointConfig)
    {
        await localStorageService.SetItemAsStringAsync($"{CustomAIEndpointConfig.CONFIG_SECTION}:ApiKey", customAIEndpointConfig.ApiKey ?? string.Empty);
        await localStorageService.SetItemAsStringAsync($"{CustomAIEndpointConfig.CONFIG_SECTION}:Endpoint", customAIEndpointConfig.Endpoint != null ? customAIEndpointConfig.Endpoint.OriginalString : string.Empty);
    }
}
