using System.Runtime.InteropServices;
using Highbyte.DotNet6502.Scripting.MoonSharp;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace Highbyte.DotNet6502.Systems.Tests.Scripting;

public sealed class LuaScreenshotTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"dotnet6502-screenshots-{Guid.NewGuid():N}");

    [Theory]
    [InlineData(PixelFormat.Rgba32, "nested/frame.png")]
    [InlineData(PixelFormat.Bgra32, "frame.PNG")]
    [InlineData(PixelFormat.Bgra32, "frame.other")]
    public void PngPreservesColorsAlphaLayerOrderOpacityAndStride(PixelFormat backgroundFormat, string filename)
    {
        var size = new RenderSize(3, 2);
        var background = new SKColor[]
        {
            new(255, 0, 0), new(0, 255, 0), new(0, 0, 255),
            SKColors.Transparent, new(200, 100, 50, 128), new(20, 40, 60),
        };
        var foreground = new SKColor[]
        {
            new(0, 0, 255, 128), SKColors.Transparent, SKColors.Transparent,
            new(255, 255, 255, 128), new(255, 0, 0), SKColors.Transparent,
        };
        var provider = new LayerProvider(
            [new(size, PixelFormat.Bgra32, 16, 0.5f, BlendMode.Normal, 10),
             new(size, backgroundFormat, 16, 1, BlendMode.Normal, 0)],
            [Pixels(foreground, 3, 2, 16, PixelFormat.Bgra32), Pixels(background, 3, 2, 16, backgroundFormat)]);
        var proxy = CreateProxy(provider);

        proxy.SaveScreenshot(filename);

        using var codec = SKCodec.Create(Path.Combine(_directory, filename));
        Assert.Equal(SKEncodedImageFormat.Png, codec.EncodedFormat);
        using var image = SKBitmap.Decode(codec, new SKImageInfo(3, 2, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        Assert.Equal(new SKColor(191, 0, 64, 255), image.GetPixel(0, 0));
        Assert.Equal(new SKColor(0, 255, 0, 255), image.GetPixel(1, 0));
        Assert.Equal(new SKColor(0, 0, 255, 255), image.GetPixel(2, 0));
        Assert.Equal(new SKColor(64, 64, 64, 64), image.GetPixel(0, 1));
        Assert.Equal(new SKColor(177, 25, 12, 191), image.GetPixel(1, 1));
        Assert.Equal(new SKColor(20, 40, 60, 255), image.GetPixel(2, 1));
    }

    [Fact]
    public void JpegSupportsBothExtensionsDefaultQualityAndQualityClamping()
    {
        const int width = 64, height = 64;
        var colors = Enumerable.Range(0, width * height)
            .Select(i => new SKColor((byte)(i * 37), (byte)(i * 13), (byte)(i * 71)))
            .ToArray();
        var proxy = CreateProxy(SingleLayer(colors, width, height));

        proxy.SaveScreenshot("default.jpg");
        proxy.SaveScreenshot("ninety.JPEG", 90);
        proxy.SaveScreenshot("low.jpg", 1);
        proxy.SaveScreenshot("clamped-low.jpg", 0);
        proxy.SaveScreenshot("high.jpeg", 100);
        proxy.SaveScreenshot("clamped-high.jpeg", 101);

        byte[] Read(string name) => File.ReadAllBytes(Path.Combine(_directory, name));
        Assert.Equal(Read("default.jpg"), Read("ninety.JPEG"));
        Assert.Equal(Read("low.jpg"), Read("clamped-low.jpg"));
        Assert.Equal(Read("high.jpeg"), Read("clamped-high.jpeg"));
        Assert.True(Read("high.jpeg").Length > Read("low.jpg").Length);
        using var codec = SKCodec.Create(Path.Combine(_directory, "default.jpg"));
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec.EncodedFormat);
        Assert.Equal(width, codec.Info.Width);
        Assert.Equal(height, codec.Info.Height);
    }

    [Theory]
    [InlineData(255, 220, 30, 10)]
    [InlineData(128, 110, 15, 5)]
    public void JpegPreservesRedAndBlueChannelsWithoutApplyingAlphaTwice(byte alpha, int red, int green, int blue)
    {
        var proxy = CreateProxy(SingleLayer(Enumerable.Repeat(new SKColor(220, 30, 10, alpha), 64).ToArray(), 8, 8));
        proxy.SaveScreenshot("red.jpg", 100);

        using var image = SKBitmap.Decode(Path.Combine(_directory, "red.jpg"));
        var color = image.GetPixel(4, 4);
        Assert.InRange((int)color.Red, red - 3, red + 3);
        Assert.InRange((int)color.Green, green - 3, green + 3);
        Assert.InRange((int)color.Blue, blue - 3, blue + 3);
    }

    [Fact]
    public void PngRetainsFullyTransparentPixels()
    {
        var proxy = CreateProxy(SingleLayer([SKColors.Transparent], 1, 1));
        proxy.SaveScreenshot("transparent.png");

        using var image = SKBitmap.Decode(Path.Combine(_directory, "transparent.png"));
        Assert.Equal(new SKColor(0, 0, 0, 0), image.GetPixel(0, 0));
    }

    [Theory]
    [InlineData("../escaped.png")]
    [InlineData("nested/../../escaped.jpg")]
    [InlineData("")]
    public void RejectsUnsafePathsBeforeWriting(string filename)
    {
        var proxy = CreateProxy(SingleLayer([SKColors.Red], 1, 1));
        Assert.Throws<ArgumentException>(() => proxy.SaveScreenshot(filename));
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public void RejectsAbsolutePathOutsideBaseDirectory()
    {
        var proxy = CreateProxy(SingleLayer([SKColors.Red], 1, 1));
        Assert.Throws<ArgumentException>(() => proxy.SaveScreenshot(_directory + "-outside/frame.png"));
        Assert.False(Directory.Exists(_directory));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void LuaRespectsFileIoAndWritePermissions(bool allowFileIo, bool allowWrite)
    {
        var host = new ScreenshotHost(new ScreenshotSystem(SingleLayer([SKColors.Red], 1, 1)));
        var adapter = new MoonSharpScriptingEngineAdapter(NullLoggerFactory.Instance, "test");
        adapter.InitializeVm(host, (_, _) => { }, new ScriptingConfig
        {
            AllowFileIO = allowFileIo,
            AllowFileWrite = allowWrite,
            FileBaseDirectory = _directory,
            AllowStore = false,
            AllowHttpRequests = false,
            AllowTcpClient = false,
        }, NullLogger.Instance, () => 0, () => 0);
        var script = adapter.LoadScript("emu.screenshot('blocked.png')", "screenshot.lua");

        var state = adapter.InitialResume(script!);

        Assert.Equal(AdapterCoroutineState.RuntimeError, state.CoroutineState);
        Assert.False(File.Exists(Path.Combine(_directory, "blocked.png")));
    }

    private LuaScreenshotProxy CreateProxy(LayerProvider provider) =>
        new(new ScreenshotHost(new ScreenshotSystem(provider)), new LuaFileProxy(_directory, allowWrite: true));

    private static LayerProvider SingleLayer(SKColor[] colors, int width, int height) =>
        new([new(new RenderSize(width, height), PixelFormat.Bgra32, width * 4, 1, BlendMode.Normal)],
            [Pixels(colors, width, height, width * 4, PixelFormat.Bgra32)]);

    private static uint[] Pixels(SKColor[] colors, int width, int height, int stride, PixelFormat format)
    {
        var bytes = new byte[height * stride];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var color = colors[y * width + x];
                var offset = y * stride + x * 4;
                bytes[offset] = format == PixelFormat.Bgra32 ? color.Blue : color.Red;
                bytes[offset + 1] = color.Green;
                bytes[offset + 2] = format == PixelFormat.Bgra32 ? color.Red : color.Blue;
                bytes[offset + 3] = color.Alpha;
            }
        return MemoryMarshal.Cast<byte, uint>(bytes).ToArray();
    }

    private sealed class ScreenshotHost(ISystem system)
        : HostApp("Test", new SystemList(), NullLoggerFactory.Instance), IHostApp
    {
        private readonly SystemRunner _runner = new(system);
        SystemRunner? IHostApp.CurrentSystemRunner => _runner;
    }

    private sealed class ScreenshotSystem(IRenderProvider provider) : GenericComputer, ISystem
    {
        IRenderProvider? ISystem.RenderProvider => provider;
    }

    private sealed class LayerProvider(IReadOnlyList<LayerInfo> layers, IReadOnlyList<ReadOnlyMemory<uint>> buffers)
        : IRenderProvider, IVideoFrameLayerProvider
    {
        public string Name => "Screenshot test layers";
        public IReadOnlyList<LayerInfo> Layers => layers;
        public IReadOnlyList<ReadOnlyMemory<uint>> CurrentFrontLayerBuffers => buffers;
        public RenderSize NativeSize => layers[0].Size;
        public PixelFormat PixelFormat => layers[0].PixelFormat;
        public int StrideBytes => layers[0].StrideBytes;
        public ReadOnlyMemory<uint> CurrentFrontBuffer => buffers[0];
        public event EventHandler? FrameCompleted { add { } remove { } }
        public void FlipBuffers() { }
        public void OnAfterInstruction() { }
        public void OnEndFrame() { }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
