using System.Net.Http;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.Generic.Input;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Impl.Skia.Generic.Video;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Microsoft.AspNetCore.WebUtilities;

namespace Highbyte.DotNet6502.App.WASM.Emulator.SystemSetup;

public class GenericComputerSetup : ISystemConfigurer<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext>
{
    public string SystemName => GenericComputer.SystemName;
    public List<string> ConfigurationVariants => s_systemVariants;

    private static readonly List<string> s_systemVariants =
    [
        "Scroll",
        "Snake",
    ];

    //private const string DEFAULT_PRG_URL = "6502binaries/Generic/Assembler/hostinteraction_scroll_text_and_cycle_colors.prg";
    //private const string DEFAULT_PRG_URL = "6502binaries/Generic/Assembler/snake6502.prg";

    private readonly BrowserContext _browserContext;
    private readonly ILoggerFactory _loggerFactory;

    public GenericComputerSetup(BrowserContext browserContext, ILoggerFactory loggerFactory)
    {
        _browserContext = browserContext;
        _loggerFactory = loggerFactory;
    }

    public IHostSystemConfig GetNewHostSystemConfig()
    {
        var genericComputerHostConfig = new GenericComputerHostConfig();
        return genericComputerHostConfig;
    }

    public async Task<ISystemConfig> GetNewConfig(string configurationVariant)
    {
        // Create default GenericComputerConfig object

        // TODO: Re-implement possibility to load 6502 program binary specified in url (in addition to the pre-configured variants)
        //// Load 6502 program binary specified in url
        //var prgBytes = await Load6502Binary(_browserContext.HttpClient, _browserContext.Uri);

        //// Get screen size specified in url
        //(var cols, var rows, var screenMemoryAddress, var colorMemoryAddress) = GetScreenSize(_browserContext.Uri);

        //cols = cols ?? 40;
        //rows = rows ?? 25;
        //screenMemoryAddress = screenMemoryAddress ?? 0x0400;
        //colorMemoryAddress = colorMemoryAddress ?? 0xd800;

        GenericComputerConfig genericComputerConfig;
        string prgUrl;
        byte[] prgBytes;
        switch (configurationVariant)
        {
            case "Scroll":
                prgUrl = "6502binaries/Generic/Assembler/hostinteraction_scroll_text_and_cycle_colors.prg";
                prgBytes = await _browserContext.HttpClient.GetByteArrayAsync(prgUrl.ToString());
                genericComputerConfig = new GenericComputerConfig
                {
                    ProgramBinary = prgBytes,

                    CPUCyclesPerFrame = 8000,
                    ScreenRefreshFrequencyHz = 60,

                    Memory = new EmulatorMemoryConfig
                    {
                        Screen = new EmulatorScreenConfig
                        {
                            Cols = 40,
                            Rows = 25,
                            BorderCols = 3,
                            BorderRows = 3,
                            ScreenStartAddress = 0x0400,
                            ScreenColorStartAddress = 0xd800,

                            UseAscIICharacters = true,
                            DefaultBgColor = 0x06,     // 0x06 = Blue
                            DefaultFgColor = 0x0e,     // 0x0e = Light blue
                            DefaultBorderColor = 0x0b, // 0x06 = Blue
                        },
                        Input = new EmulatorInputConfig
                        {
                            KeyPressedAddress = 0xd030,
                            KeyDownAddress = 0xd031,
                            KeyReleasedAddress = 0xd031,
                        }
                    }
                };
                break;

            case "Snake":
                prgUrl = "6502binaries/Generic/Assembler/snake6502.prg";
                prgBytes = await _browserContext.HttpClient.GetByteArrayAsync(prgUrl.ToString());
                genericComputerConfig = new GenericComputerConfig
                {
                    ProgramBinary = prgBytes,
                    ScreenRefreshFrequencyHz = 60,
                    StopAtBRK = false,

                    Memory = new EmulatorMemoryConfig
                    {
                        Screen = new EmulatorScreenConfig
                        {
                            Cols = 32,
                            Rows = 32,
                            BorderCols = 3,
                            BorderRows = 3,
                            ScreenStartAddress = 0x0200,
                            ScreenColorStartAddress = 0xd800,   // Not used with this program

                            ScreenRefreshStatusAddress = 0xd000, // The 6502 code should set bit 1 here when it's done for current frame

                            DefaultBgColor = 0x00,     // 0x00 = Black
                            DefaultFgColor = 0x01,     // 0x01 = White
                            DefaultBorderColor = 0x0b, // 0x0b = Dark grey

                            UseAscIICharacters = false,
                            CharacterMap = new Dictionary<string, byte>
                            {
                                { "10", 32 },
                                { "13", 32 },
                                { "160", 219 },
                                { "224", 219 },
                            }
                        },
                        Input = new EmulatorInputConfig
                        {
                            KeyPressedAddress = 0xd030,
                            KeyDownAddress = 0xd031,
                            KeyReleasedAddress = 0xd031,
                        }
                    }
                };
                break;

            default:
                throw new ArgumentException($"Unknown configuration variant '{configurationVariant}'.");
                break;
        }

        //genericComputerConfig.Validate();

        return genericComputerConfig;
    }

    public Task PersistConfig(ISystemConfig systemConfig)
    {
        var genericComputerConfig = (GenericComputerConfig)systemConfig;
        // TODO: Save config settings to browser local storage
        return Task.CompletedTask;
    }

    public ISystem BuildSystem(ISystemConfig systemConfig)
    {
        var genericComputerConfig = (GenericComputerConfig)systemConfig;
        return GenericComputerBuilder.SetupGenericComputerFromConfig(genericComputerConfig, _loggerFactory);
    }

    public Task<SystemRunner> BuildSystemRunner(
        ISystem system,
        ISystemConfig systemConfig,
        IHostSystemConfig hostSystemConfig,
        SkiaRenderContext renderContext,
        AspNetInputHandlerContext inputHandlerContext,
        WASMAudioHandlerContext audioHandlerContext)
    {
        var genericComputer = (GenericComputer)system;
        var genericComputerConfig = (GenericComputerConfig)systemConfig;

        var renderer = new GenericComputerSkiaRenderer(genericComputer, renderContext, genericComputerConfig.Memory.Screen);
        var inputHandler = new GenericComputerAspNetInputHandler(genericComputer, inputHandlerContext, genericComputerConfig.Memory.Input);
        var audioHandler = new NullAudioHandler(genericComputer);

        return Task.FromResult(new SystemRunner(genericComputer, renderer, inputHandler, audioHandler));
    }

    private (int? cols, int? rows, ushort? screenMemoryAddress, ushort? colorMemoryAddress) GetScreenSize(Uri uri)
    {
        int? cols = null;
        int? rows = null;
        ushort? screenMemoryAddress = null;
        ushort? colorMemoryAddress = null;

        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("cols", out var colsParameter))
        {
            if (int.TryParse(colsParameter, out var colsParsed))
                cols = colsParsed;
            else
                cols = null;
        }
        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("rows", out var rowsParameter))
        {
            if (int.TryParse(rowsParameter, out var rowsParsed))
                rows = rowsParsed;
            else
                rows = null;
        }
        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("screenMem", out var screenMemParameter))
        {
            if (ushort.TryParse(screenMemParameter, out var screenMemParsed))
                screenMemoryAddress = screenMemParsed;
            else
                screenMemoryAddress = null;
        }
        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("colorMem", out var colorMemParameter))
        {
            if (ushort.TryParse(colorMemParameter, out var colorMemParsed))
                colorMemoryAddress = colorMemParsed;
            else
                colorMemoryAddress = null;
        }

        return (cols, rows, screenMemoryAddress, colorMemoryAddress);

    }

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
