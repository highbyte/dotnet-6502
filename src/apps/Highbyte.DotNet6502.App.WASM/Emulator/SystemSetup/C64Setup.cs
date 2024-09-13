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

namespace Highbyte.DotNet6502.App.WASM.Emulator.SystemSetup;

public class C64Setup : ISystemConfigurer<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext>
{
    public string SystemName => C64.SystemName;
    public List<string> ConfigurationVariants => s_systemVariants;

    private static readonly List<string> s_systemVariants = C64ModelInventory.C64Models.Keys.ToList();


    private const string LOCAL_STORAGE_ROM_PREFIX = "rom_";
    private readonly BrowserContext _browserContext;
    private readonly ILoggerFactory _loggerFactory;

    public C64Setup(BrowserContext browserContext, ILoggerFactory loggerFactory)
    {
        _browserContext = browserContext;
        _loggerFactory = loggerFactory;
    }


    public IHostSystemConfig GetNewHostSystemConfig()
    {
        var c64HostConfig = new C64HostConfig
        {
            Renderer = C64HostRenderer.SkiaSharp,
        };
        return c64HostConfig;
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

        var apiConfig = await GetOpenAIConfig(_browserContext.LocalStorage);
        var openAICodeSuggestion = new OpenAICodeSuggestion(apiConfig, "Commodore 64 Basic");
        var c64BasicCodingAssistant = new C64BasicCodingAssistant(c64, openAICodeSuggestion, _loggerFactory);
        // TODO: // c64HostConfig.BasicAIAssistantDefaultEnabled
        var inputHandler = new C64AspNetInputHandler(c64, inputHandlerContext, _loggerFactory, c64HostConfig.InputConfig, c64BasicCodingAssistant, true); // c64HostConfig.BasicAIAssistantDefaultEnabled

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

    private async Task<ApiConfig> GetOpenAIConfig(ILocalStorageService localStorageService)
    {
        var enabled = await localStorageService.GetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:Enabled");
        bool.TryParse(enabled, out bool enabledBool);
        var apiKey = await localStorageService.GetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:ApiKey");
        var deploymentName = await localStorageService.GetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:DeploymentName");
        var endpoint = await localStorageService.GetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:Endpoint");

        var selfHosted = await localStorageService.GetItemAsStringAsync($"{ApiConfig.CONFIG_SECTION}:SelfHosted");
        bool.TryParse(selfHosted, out bool selfHostedBool);

        var endPointUri = !string.IsNullOrEmpty(endpoint) ? new Uri(endpoint) : null;

        var apiConfig = new ApiConfig()
        {
            Enabled = string.IsNullOrEmpty(enabled) ? !string.IsNullOrEmpty(apiKey) : enabledBool,
            ApiKey = apiKey,
            DeploymentName = deploymentName,
            Endpoint = endPointUri,
            SelfHosted = selfHostedBool,
        };
        return apiConfig;
    }
}
