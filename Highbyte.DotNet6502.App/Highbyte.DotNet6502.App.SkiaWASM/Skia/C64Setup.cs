using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.Commodore64;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Impl.Skia.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.App.SkiaWASM.Skia;

public class C64Setup
{
    const string LOCAL_STORAGE_ROM_PREFIX = "rom_";
    private readonly BrowserContext _browserContext;

    public C64Setup(BrowserContext browserContext)
    {
        _browserContext = browserContext;
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
        var c64 = C64.BuildC64(c64Config);
        return c64;
    }

    public SystemRunner BuildSystemRunner(
        ISystem system,
        ISystemConfig systemConfig,
        SkiaRenderContext renderContext,
        AspNetInputHandlerContext inputHandlerContext,
        WASMSoundHandlerContext soundHandlerContext
        )
    {
        var renderer = new C64SkiaRenderer();
        var inputHandler = new C64AspNetInputHandler();
        var soundHandler = new C64WASMSoundHandler();

        var c64 = (C64)system;
        renderer.Init(c64, renderContext);
        inputHandler.Init(c64, inputHandlerContext);
        soundHandler.Init(c64, soundHandlerContext);

        var systemRunnerBuilder = new SystemRunnerBuilder<C64, SkiaRenderContext, AspNetInputHandlerContext, WASMSoundHandlerContext>(c64);
        var systemRunner = systemRunnerBuilder
            .WithRenderer(renderer)
            .WithInputHandler(inputHandler)
            .WithSoundHandler(soundHandler)
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
