using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.Commodore64.Audio;
using Highbyte.DotNet6502.Impl.AspNet.Commodore64.Input;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Impl.Skia.Commodore64.Video.v1;
using Highbyte.DotNet6502.Impl.Skia.Commodore64.Video.v2;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Blazored.LocalStorage;
using Highbyte.DotNet6502.AI.CodingAssistant.Inference.OpenAI;
using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.Systems.Commodore64.Utils.BasicAssistant;
using static Highbyte.DotNet6502.AI.CodingAssistant.CustomAIEndpointCodeSuggestion;
using System.Text.Json;

namespace Highbyte.DotNet6502.App.WASM.Emulator.SystemSetup;

public class C64Setup : ISystemConfigurer<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext>
{
    public string SystemName => C64.SystemName;
    public Task<List<string>> GetConfigurationVariants(IHostSystemConfig hostSystemConfig) => Task.FromResult(s_systemVariants);

    private static readonly List<string> s_systemVariants = C64ModelInventory.C64Models.Keys.ToList();

    private const string LEGACY_LOCAL_STORAGE_ROM_PREFIX = "rom_";

    private readonly BrowserContext _browserContext;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<C64Setup> _logger;
    private const string DEFAULT_CUSTOMENDPOINT = "https://highbyte-dotnet6502-codecompletion.azurewebsites.net/";
    // Note: For now, use a public visible key as default just to prevent at least some random users to access the endpoint...
    private const string DEFAULT_CUSTOMENDPOINT_APIKEY = "9fe8f8161c1d43251a46bb576336a1a25d7ab607cb5a1b4b960c0949d87bced7";

    public C64Setup(BrowserContext browserContext, ILoggerFactory loggerFactory)
    {
        _browserContext = browserContext;
        _loggerFactory = loggerFactory;

        _logger = loggerFactory.CreateLogger<C64Setup>();
    }

    public async Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        var configKey = $"{C64HostConfig.ConfigSectionName}";
        var c64HostConfigJson = await _browserContext.LocalStorage.GetItemAsStringAsync(configKey);

        C64HostConfig? c64HostConfig = null;
        if (!string.IsNullOrEmpty(c64HostConfigJson))
        {
            try
            {
                c64HostConfig = JsonSerializer.Deserialize<C64HostConfig>(c64HostConfigJson)!;

                // Note: Because ROMDirectory should nevery be used when running WASM, make sure it is empty (regardless if config from local storage has it set).
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

    public async Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        var c64HostConfig = (C64HostConfig)hostSystemConfig;
        await _browserContext.LocalStorage.SetItemAsStringAsync($"{C64HostConfig.ConfigSectionName}", JsonSerializer.Serialize(c64HostConfig));
    }

    public Task<ISystem> BuildSystem(string configurationVariant, IHostSystemConfig hostSystemConfig)
    {
        var c64HostSystemConfig = (C64HostConfig)hostSystemConfig;
        var c64Config = new C64Config
        {
            C64Model = configurationVariant,
            Vic2Model = C64ModelInventory.C64Models[configurationVariant].Vic2Models.First().Name, // NTSC, NTSC_old, PAL
            AudioEnabled = c64HostSystemConfig.SystemConfig.AudioEnabled,
            ROMs = c64HostSystemConfig.SystemConfig.ROMs,
            ROMDirectory = c64HostSystemConfig.SystemConfig.ROMDirectory,
        };

        var c64 = C64.BuildC64(c64Config, _loggerFactory);
        return Task.FromResult<ISystem>(c64);
    }

    public async Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        IHostSystemConfig hostSystemConfig,
        SkiaRenderContext renderContext,
        AspNetInputHandlerContext inputHandlerContext,
        WASMAudioHandlerContext audioHandlerContext
        )
    {

        var c64 = (C64)system;
        var c64HostConfig = (C64HostConfig)hostSystemConfig;

        IRenderer renderer;
        switch (c64HostConfig.Renderer)
        {
            case C64HostRenderer.SkiaSharp:
                renderer = new C64SkiaRenderer(c64, renderContext);
                break;
            case C64HostRenderer.SkiaSharp2:
                renderer = new C64SkiaRenderer2(c64, renderContext);
                break;
            case C64HostRenderer.SkiaSharp2b:
                renderer = new C64SkiaRenderer2b(c64, renderContext);
                break;
            default:
                throw new NotImplementedException($"Renderer {c64HostConfig.Renderer} not implemented.");
        }

        var codeSuggestion = await GetCodeSuggestionImplementation(c64HostConfig, _browserContext.LocalStorage);
        var c64BasicCodingAssistant = new C64BasicCodingAssistant(c64, codeSuggestion, _loggerFactory);
        var inputHandler = new C64AspNetInputHandler(c64, inputHandlerContext, _loggerFactory, c64HostConfig.InputConfig, c64BasicCodingAssistant, c64HostConfig.BasicAIAssistantDefaultEnabled);

        var audioHandler = new C64WASMAudioHandler(c64, audioHandlerContext, _loggerFactory);

        return new SystemRunner(c64, renderer, inputHandler, audioHandler);
    }

    private async Task<List<ROM>> GetROMsFromLocalStorageLegacy()
    {
        var roms = new List<ROM>();
        string name;
        byte[] data;

        name = C64SystemConfig.BASIC_ROM_NAME;
        data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LEGACY_LOCAL_STORAGE_ROM_PREFIX}{name}");
        if (data != null)
        {
            roms.Add(new ROM
            {
                Name = name,
                Data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LEGACY_LOCAL_STORAGE_ROM_PREFIX}{name}"),
            });
        }
        name = C64SystemConfig.KERNAL_ROM_NAME;
        data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LEGACY_LOCAL_STORAGE_ROM_PREFIX}{name}");
        if (data != null)
        {
            roms.Add(new ROM
            {
                Name = name,
                Data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LEGACY_LOCAL_STORAGE_ROM_PREFIX}{name}"),
            });
        }
        name = C64SystemConfig.CHARGEN_ROM_NAME;
        data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LEGACY_LOCAL_STORAGE_ROM_PREFIX}{name}");
        if (data != null)
        {
            roms.Add(new ROM
            {
                Name = name,
                Data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LEGACY_LOCAL_STORAGE_ROM_PREFIX}{name}"),
            });
        }

        return roms;
    }

    public async Task<byte[]> GetROMFromUrl(HttpClient httpClient, string url)
    {
        return await httpClient.GetByteArrayAsync(url);

        //var request = new HttpRequestMessage(HttpMethod.Get, url);
        ////request.SetBrowserRequestMode(BrowserRequestMode.NoCors);
        ////request.SetBrowserRequestCache(BrowserRequestCache.NoStore); //optional  

        ////var response = await HttpClient!.SendAsync(request);

        //var statusCode = response.StatusCode;
        //response.EnsureSuccessStatusCode();
        //byte[] responseRawData = await response.Content.ReadAsByteArrayAsync();
        //return responseRawData;
    }

    public static async Task<ICodeSuggestion> GetCodeSuggestionImplementation(C64HostConfig c64HostConfig, ILocalStorageService localStorageService, bool defaultToNoneIdConfigError = true)
    {
        ICodeSuggestion codeSuggestion;
        try
        {
            if (c64HostConfig.CodeSuggestionBackendType == CodeSuggestionBackendTypeEnum.OpenAI)
            {
                var openAIApiConfig = await GetOpenAIConfig(localStorageService);
                codeSuggestion = new OpenAICodeSuggestion(openAIApiConfig, C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION);
            }
            else if (c64HostConfig.CodeSuggestionBackendType == CodeSuggestionBackendTypeEnum.CustomEndpoint)
            {
                var customAIEndpointConfig = await GetCustomAIEndpointConfig(localStorageService);
                codeSuggestion = new CustomAIEndpointCodeSuggestion(customAIEndpointConfig, C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION);
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
        catch (Exception ex)
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
            deploymentName = "gpt-4o";  // Default to a model that works well

        var endpoint = await localStorageService.GetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:Endpoint");
        Uri.TryCreate(endpoint, UriKind.Absolute, out var endPointUri);

        var selfHosted = await localStorageService.GetItemAsync<bool>($"{ApiConfig.CONFIG_SECTION}:SelfHosted");

        var apiConfig = new ApiConfig()
        {
            ApiKey = apiKey,    // Api key for OpenAI (required), Azure OpenAI (required), or SelfHosted (optional).
            DeploymentName = deploymentName, // AI model name
            Endpoint = endPointUri,     // Used if using Azure OpenAI, or SelfHosted
            SelfHosted = selfHosted, // Set to true to use self-hosted OpenAI API compatible endpoint.
        };
        return apiConfig;
    }

    public static async Task SaveOpenAICodingAssistantConfigToLocalStorage(ILocalStorageService localStorageService, ApiConfig apiConfig)
    {
        await localStorageService.SetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:ApiKey", apiConfig.ApiKey);
        await localStorageService.SetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:DeploymentName", apiConfig.DeploymentName);
        await localStorageService.SetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:Endpoint", apiConfig.Endpoint != null ? apiConfig.Endpoint.OriginalString : "");
        await localStorageService.SetItemAsync($"{ApiConfig.CONFIG_SECTION}:SelfHosted", apiConfig.SelfHosted);
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

    public static async Task SaveCustomCodingAssistantConfigToLocalStorage(ILocalStorageService localStorageService, CustomAIEndpointConfig customAIEndpointConfig)
    {
        await localStorageService.SetItemAsStringAsync($"{CustomAIEndpointConfig.CONFIG_SECTION}:ApiKey", customAIEndpointConfig.ApiKey);
        await localStorageService.SetItemAsStringAsync($"{CustomAIEndpointConfig.CONFIG_SECTION}:Endpoint", customAIEndpointConfig.Endpoint != null ? customAIEndpointConfig.Endpoint.OriginalString : "");
    }
}
