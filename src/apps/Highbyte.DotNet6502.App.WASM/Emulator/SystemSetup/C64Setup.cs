using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.Commodore64.Audio;
using Highbyte.DotNet6502.Impl.AspNet.Commodore64.Input;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Video;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Impl.Skia.Commodore64.Video;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.App.WASM.Emulator.SystemSetup;

public class C64Setup : SystemConfigurer<WASMRenderContextContainer, AspNetInputHandlerContext, WASMAudioHandlerContext>
{
    public string SystemName => C64.SystemName;

    private const string LOCAL_STORAGE_ROM_PREFIX = "rom_";
    private readonly BrowserContext _browserContext;
    private readonly ILoggerFactory _loggerFactory;
    private readonly C64HostConfig _hostConfig;
    private readonly EmulatorConfig _emulatorConfig;

    public C64Setup(BrowserContext browserContext, ILoggerFactory loggerFactory, C64HostConfig c64HostConfig, EmulatorConfig emulatorConfig)
    {
        _browserContext = browserContext;
        _loggerFactory = loggerFactory;
        _hostConfig = c64HostConfig;
        _emulatorConfig = emulatorConfig;
    }

    public async Task<ISystemConfig> GetNewConfig(string configurationVariant)
    {
        var romList = await GetROMsFromLocalStorage();
        var c64Config = new C64Config
        {
            C64Model = "C64NTSC",   // C64NTSC, C64PAL
            Vic2Model = "NTSC",     // NTSC, NTSC_old, PAL
            // C64Model = "C64PAL",   // C64NTSC, C64PAL
            // Vic2Model = "PAL",     // NTSC, NTSC_old, PAL

            ROMDirectory = "",  // Set ROMDirectory to skip loading ROMs from file system (ROMDirectory + File property), instead read from the Data property
            ROMs = romList,

            AudioSupported = true,
            AudioEnabled = false,   // Audio disabled by default until playback is more stable
        };

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

    public Task<IHostSystemConfig> GetHostSystemConfig()
    {
        return Task.FromResult((IHostSystemConfig)_hostConfig);
    }

    public SystemRunner BuildSystemRunner(
        ISystem system,
        ISystemConfig systemConfig,
        IHostSystemConfig hostSystemConfig,
        WASMRenderContextContainer renderContextContainer,
        AspNetInputHandlerContext inputHandlerContext,
        WASMAudioHandlerContext audioHandlerContext
        )
    {
        var c64HostConfig = (C64HostConfig)hostSystemConfig;

        IRenderer renderer;
        IRenderContext renderContext;
        switch (_emulatorConfig.Renderer)
        {
            case RendererType.SkiaSharp:
                renderer = new C64SkiaRenderer();
                renderContext = renderContextContainer.SkiaRenderContext;
                break;
            case RendererType.SilkNetOpenGl:
                renderer = new C64SilkNetOpenGlRenderer(c64HostConfig.SilkNetOpenGlRendererConfig);
                renderContext = renderContextContainer.SilkNetOpenGlRenderContext;
                break;
            default:
                throw new NotImplementedException($"Renderer {_emulatorConfig.Renderer} not implemented.");
        }

        var inputHandler = new C64AspNetInputHandler(_loggerFactory, _hostConfig.InputConfig);
        var audioHandler = new C64WASMAudioHandler(_loggerFactory);

        var c64 = (C64)system;
        renderer.Init(c64, renderContext);
        inputHandler.Init(c64, inputHandlerContext);
        audioHandler.Init(c64, audioHandlerContext);

        var systemRunnerBuilder = new SystemRunnerBuilder<C64, SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext>(c64);
        var systemRunner = systemRunnerBuilder
            .WithRenderer(renderer)
            .WithInputHandler(inputHandler)
            .WithAudioHandler(audioHandler)
            .Build();
        return systemRunner;
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
                Data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LOCAL_STORAGE_ROM_PREFIX}{name}")
            });
        }
        name = C64Config.KERNAL_ROM_NAME;
        data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LOCAL_STORAGE_ROM_PREFIX}{name}");
        if (data != null)
        {
            roms.Add(new ROM
            {
                Name = name,
                Data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LOCAL_STORAGE_ROM_PREFIX}{name}")
            });
        }
        name = C64Config.CHARGEN_ROM_NAME;
        data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LOCAL_STORAGE_ROM_PREFIX}{name}");
        if (data != null)
        {
            roms.Add(new ROM
            {
                Name = name,
                Data = await _browserContext.LocalStorage.GetItemAsync<byte[]>($"{LOCAL_STORAGE_ROM_PREFIX}{name}")
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
}
