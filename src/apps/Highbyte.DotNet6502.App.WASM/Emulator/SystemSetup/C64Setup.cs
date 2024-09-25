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

namespace Highbyte.DotNet6502.App.WASM.Emulator.SystemSetup;

public class C64Setup : ISystemConfigurer<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext>
{
    public string SystemName => C64.SystemName;
    public List<string> ConfigurationVariants => s_systemVariants;

    private static readonly List<string> s_systemVariants = C64ModelInventory.C64Models.Keys.ToList();


    private const string LOCAL_STORAGE_ROM_PREFIX = "rom_";
    private readonly BrowserContext _browserContext;
    private readonly ILoggerFactory _loggerFactory;

    private const string DEFAULT_CUSTOMENDPOINT = "https://highbyte-dotnet6502-codecompletion.azurewebsites.net/";
    // Note: For now, use a public visible key as default just to prevent at least some random users to access the endpoint...
    private const string DEFAULT_CUSTOMENDPOINT_APIKEY = "9fe8f8161c1d43251a46bb576336a1a25d7ab607cb5a1b4b960c0949d87bced7";

    public C64Setup(BrowserContext browserContext, ILoggerFactory loggerFactory)
    {
        _browserContext = browserContext;
        _loggerFactory = loggerFactory;
    }

    public async Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        var c64HostConfig = new C64HostConfig();
        await LoadHostConfig(c64HostConfig);
        return c64HostConfig;
    }

    public async Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        await SaveHostConfig((C64HostConfig)hostSystemConfig);
    }

    public async Task<ISystemConfig> GetNewConfig(string configurationVariant)
    {
        if (!s_systemVariants.Contains(configurationVariant))
            throw new ArgumentException($"Unknown configuration variant '{configurationVariant}'.");

        var romList = await GetROMsFromLocalStorage();

        var c64Config = new C64Config
        {
            C64Model = configurationVariant,
            Vic2Model = C64ModelInventory.C64Models[configurationVariant].Vic2Models.First().Name, // NTSC, NTSC_old, PAL

            ROMDirectory = "",  // Set ROMDirectory to skip loading ROMs from file system (ROMDirectory + File property), instead read from the Data property
            ROMs = romList,

            AudioSupported = true,
            AudioEnabled = false,   // Audio disabled by default until playback is more stable

            InstrumentationEnabled = false, // Start with instrumentation off by default
        };

        c64Config.SetROMDefaultChecksums();

        //c64Config.Validate();

        return c64Config;
    }

    public async Task PersistConfig(ISystemConfig systemConfig)
    {
        var c64Config = (C64Config)systemConfig;
        await SaveROMsToLocalStorage(c64Config.ROMs);
    }

    public ISystem BuildSystem(ISystemConfig systemConfig)
    {
        var c64Config = (C64Config)systemConfig;
        var c64 = C64.BuildC64(c64Config, _loggerFactory);
        return c64;
    }

    public async Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        ISystemConfig systemConfig,
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

    private async Task<List<ROM>> GetROMsFromLocalStorage()
    {
        var roms = new List<ROM>();
        string name;
        byte[] data;

        name = C64Config.BASIC_ROM_NAME;
        data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LOCAL_STORAGE_ROM_PREFIX}{name}");
        if (data != null)
        {
            roms.Add(new ROM
            {
                Name = name,
                Data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LOCAL_STORAGE_ROM_PREFIX}{name}"),
            });
        }
        name = C64Config.KERNAL_ROM_NAME;
        data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LOCAL_STORAGE_ROM_PREFIX}{name}");
        if (data != null)
        {
            roms.Add(new ROM
            {
                Name = name,
                Data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LOCAL_STORAGE_ROM_PREFIX}{name}"),
            });
        }
        name = C64Config.CHARGEN_ROM_NAME;
        data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LOCAL_STORAGE_ROM_PREFIX}{name}");
        if (data != null)
        {
            roms.Add(new ROM
            {
                Name = name,
                Data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LOCAL_STORAGE_ROM_PREFIX}{name}"),
            });
        }

        return roms;
    }

    private async Task SaveROMsToLocalStorage(List<ROM> roms)
    {
        foreach (var requiredRomName in C64Config.RequiredROMs)
        {
            var rom = roms.SingleOrDefault(x => x.Name == requiredRomName);
            if (rom != null)
                await _browserContext.LocalStorage.SetItemAsync($"{LOCAL_STORAGE_ROM_PREFIX}{rom.Name}", rom.Data);
            else
                await _browserContext.LocalStorage.RemoveItemAsync($"{LOCAL_STORAGE_ROM_PREFIX}{requiredRomName}");
        }
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

    private async Task<C64HostConfig> LoadHostConfig(C64HostConfig c64HostConfig)
    {
        var codeSuggestionBackendTypeString = await _browserContext.LocalStorage.GetItemAsStringAsync($"{C64HostConfig.ConfigSectionName}:CodeSuggestionBackendType");
        if (string.IsNullOrEmpty(codeSuggestionBackendTypeString))
            codeSuggestionBackendTypeString = CodeSuggestionBackendTypeEnum.CustomEndpoint.ToString();
        Enum.TryParse(codeSuggestionBackendTypeString, out CodeSuggestionBackendTypeEnum codeSuggestionBackendType);
        c64HostConfig.CodeSuggestionBackendType = codeSuggestionBackendType;

        var rendererString = await _browserContext.LocalStorage.GetItemAsStringAsync($"{C64HostConfig.ConfigSectionName}:Renderer");
        if (string.IsNullOrEmpty(rendererString))
            rendererString = C64HostRenderer.SkiaSharp.ToString();
        Enum.TryParse(rendererString, out C64HostRenderer renderer);
        c64HostConfig.Renderer = renderer;

        return c64HostConfig;
    }

    private async Task SaveHostConfig(C64HostConfig c64HostConfig)
    {
        await _browserContext.LocalStorage.SetItemAsStringAsync($"{C64HostConfig.ConfigSectionName}:CodeSuggestionBackendType", c64HostConfig.CodeSuggestionBackendType.ToString());
        await _browserContext.LocalStorage.SetItemAsStringAsync($"{C64HostConfig.ConfigSectionName}:Renderer", c64HostConfig.Renderer.ToString());
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
