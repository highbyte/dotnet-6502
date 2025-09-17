using System.Text.Json;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.Generic.Input;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.App.WASM.Emulator.SystemSetup;

public class GenericComputerSetup : ISystemConfigurer<AspNetInputHandlerContext, WASMAudioHandlerContext>
{
    public string SystemName => GenericComputer.SystemName;

    public Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig)
    {
        var examplePrograms = ((GenericComputerSystemConfig)systemConfig).ExamplePrograms.Keys.OrderByDescending(x => x).ToList();
        return Task.FromResult(examplePrograms);
    }

    //private const string DEFAULT_PRG_URL = "6502binaries/Generic/Assembler/hostinteraction_scroll_text_and_cycle_colors.prg";
    //private const string DEFAULT_PRG_URL = "6502binaries/Generic/Assembler/snake6502.prg";

    private readonly BrowserContext _browserContext;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<GenericComputerSetup> _logger;

    public GenericComputerSetup(BrowserContext browserContext, ILoggerFactory loggerFactory)
    {
        _browserContext = browserContext;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<GenericComputerSetup>();
    }

    public async Task<IHostSystemConfig> GetNewHostSystemConfig()
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

            genericComputerHostConfig.SystemConfig.ExamplePrograms = new Dictionary<string, string>
            {
                { "Scroll", "6502binaries/Generic/Assembler/hostinteraction_scroll_text_and_cycle_colors.prg" },
                { "Snake", "6502binaries/Generic/Assembler/snake6502.prg" },
            };
        }

        return genericComputerHostConfig;
    }

    public async Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
    {
        var cenericComputerHostConfig = (GenericComputerHostConfig)hostSystemConfig;
        await _browserContext.LocalStorage.SetItemAsStringAsync($"{GenericComputerHostConfig.ConfigSectionName}", JsonSerializer.Serialize(cenericComputerHostConfig));
    }

    public async Task<ISystem> BuildSystem(string configurationVariant, ISystemConfig systemConfig)
    {
        var genericComputerSystemConfig = (GenericComputerSystemConfig)systemConfig;
        var exampleProgramPath = genericComputerSystemConfig.ExamplePrograms[configurationVariant];
        var exampleProgramBytes = await _browserContext.HttpClient.GetByteArrayAsync(exampleProgramPath);
        var genericComputerConfig = GenericComputerExampleConfigs.GetExampleConfig(configurationVariant, genericComputerSystemConfig, exampleProgramBytes);

        return GenericComputerBuilder.SetupGenericComputerFromConfig(genericComputerConfig, _loggerFactory);
    }

    public Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        IHostSystemConfig hostSystemConfig,
        AspNetInputHandlerContext inputHandlerContext,
        WASMAudioHandlerContext audioHandlerContext)
    {
        var genericComputer = (GenericComputer)system;
        var genericComputerHostConfig = (GenericComputerHostConfig)hostSystemConfig;
        var genericComputerConfig = genericComputerHostConfig.SystemConfig;

        var inputHandler = new GenericComputerAspNetInputHandler(genericComputer, inputHandlerContext, genericComputer.GenericComputerConfig.Memory.Input);
        var audioHandler = new NullAudioHandler(genericComputer);

        return Task.FromResult(new SystemRunner(genericComputer, inputHandler, audioHandler));
    }

    //private (int? cols, int? rows, ushort? screenMemoryAddress, ushort? colorMemoryAddress) GetScreenSize(Uri uri)
    //{
    //    int? cols = null;
    //    int? rows = null;
    //    ushort? screenMemoryAddress = null;
    //    ushort? colorMemoryAddress = null;

    //    if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("cols", out var colsParameter))
    //    {
    //        if (int.TryParse(colsParameter, out var colsParsed))
    //            cols = colsParsed;
    //        else
    //            cols = null;
    //    }
    //    if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("rows", out var rowsParameter))
    //    {
    //        if (int.TryParse(rowsParameter, out var rowsParsed))
    //            rows = rowsParsed;
    //        else
    //            rows = null;
    //    }
    //    if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("screenMem", out var screenMemParameter))
    //    {
    //        if (ushort.TryParse(screenMemParameter, out var screenMemParsed))
    //            screenMemoryAddress = screenMemParsed;
    //        else
    //            screenMemoryAddress = null;
    //    }
    //    if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("colorMem", out var colorMemParameter))
    //    {
    //        if (ushort.TryParse(colorMemParameter, out var colorMemParsed))
    //            colorMemoryAddress = colorMemParsed;
    //        else
    //            colorMemoryAddress = null;
    //    }

    //    return (cols, rows, screenMemoryAddress, colorMemoryAddress);

    //}

    //private async Task<byte[]> Load6502Binary(HttpClient httpClient, Uri uri)
    //{
    //    byte[] prgBytes;
    //    if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("prgEnc", out var prgEnc))
    //        // Query parameter prgEnc must be a valid Base64Url encoded string.
    //        // Examples on how to generate it from a compiled 6502 binary file:
    //        //      Linux: 
    //        //          base64 -w0 myprogram.prg | sed 's/+/-/g; s/\//_/g'

    //        //      https://www.base64encode.org/   
    //        //          - Encode files to Base64 format
    //        //          - Select file
    //        //          - Select options: BINARY, Perform URL-safe encoding (uses Base64Url format)
    //        //          - ENCODE
    //        //          - CLICK OR TAP HERE to download the encoded file
    //        //          - Use the contents in the generated file.
    //        //
    //        // Examples to generate a QR Code that will launch the program in the Base64URL string above:
    //        //      Linux:
    //        //          qrencode -s 3 -l L -o "myprogram.png" "http://localhost:5000/?prgEnc=THE_PROGRAM_ENCODED_AS_BASE64URL"
    //        prgBytes = Base64UrlDecode(prgEnc.ToString());
    //    else if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("prgUrl", out var prgUrl))
    //    {
    //        prgBytes = await httpClient.GetByteArrayAsync(prgUrl.ToString());
    //    }
    //    else
    //    {
    //        prgBytes = await httpClient.GetByteArrayAsync(DEFAULT_PRG_URL);
    //    }

    //    return prgBytes;
    //}

    ///// <summary>
    ///// Decode a Base64Url encoded string.
    ///// The Base64Url standard is a bit different from normal Base64
    ///// - Replaces '+' with '-'
    ///// - Replaces '/' with '_'
    ///// - Removes trailing '=' padding
    ///// 
    ///// This method does the above in reverse before decoding it as a normal Base64 string.
    ///// </summary>
    ///// <param name="arg"></param>
    ///// <returns></returns>
    //private byte[] Base64UrlDecode(string arg)
    //{
    //    var s = arg;
    //    s = s.Replace('-', '+'); // 62nd char of encoding
    //    s = s.Replace('_', '/'); // 63rd char of encoding
    //    switch (s.Length % 4) // Pad with trailing '='s
    //    {
    //        case 0: break; // No pad chars in this case
    //        case 2: s += "=="; break; // Two pad chars
    //        case 3: s += "="; break; // One pad char
    //        default:
    //            throw new ArgumentException("Illegal base64url string!");
    //    }
    //    return Convert.FromBase64String(s); // Standard base64 decoder
    //}
}
