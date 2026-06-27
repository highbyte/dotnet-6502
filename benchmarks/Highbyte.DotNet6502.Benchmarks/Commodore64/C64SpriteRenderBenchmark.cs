using BenchmarkDotNet.Attributes;
using Highbyte.DotNet6502.Benchmarks.Commodore64.Data;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Benchmarks.Commodore64;

/// <summary>
/// Compares Vic2Rasterizer sprite rendering cost: end-of-frame (per-frame) vs per-raster-line
/// (multiplex-capable), for the SAME 8 static sprites (mix of single/multi color and X/Y expand).
///
/// One frame is simulated by advancing the VIC-II raster in instruction-sized cycle chunks and
/// calling the rasterizer's OnAfterInstruction each step, then OnEndFrame - no CPU execution, so
/// only the rendering path (incl. the per-line latch/snapshot overhead, and for per-frame the
/// StoreRasterLineIORegisters snapshot cost) is measured. The sprites are identical in both modes,
/// so the delta is the per-mode rendering overhead.
/// </summary>
[MemoryDiagnoser]
public class C64SpriteRenderBenchmark
{
    // false = end-of-frame sprites (old path); true = per-raster-line sprites (multiplex path).
    [Params(false, true)]
    public bool PerLineSprites;

    // 0 = background only (isolates the per-line fixed scan overhead + the per-frame snapshot cost);
    // 8 = full scene. Marginal sprite cost per mode = (8 - 0).
    [Params(0, 8)]
    public int NumberOfSprites;

    // false = fully-filled opaque sprites (heaviest pixel load);
    // true  = sparse, realistic sprites (~half the rows empty, partial fill). No effect at 0 sprites.
    [Params(false, true)]
    public bool Sparse;

    private C64 _c64 = default!;
    private Vic2Rasterizer _rasterizer = default!;
    private ulong _cyclesPerFrame;

    [GlobalSetup]
    public void Setup()
    {
        var c64Config = new C64Config
        {
            C64Model = "C64NTSC",
            Vic2Model = "NTSC",
            LoadROMs = false,
            TimerMode = TimerMode.UpdateEachRasterLine,
            AudioEnabled = false,
            InstrumentationEnabled = false,
        };
        _c64 = C64.BuildC64(c64Config, new NullLoggerFactory());

        // ---- 8 static, on-screen sprites: mix of single/multi color and X/Y expansion ----
        // (x, y) are sprite registers; all <= 255 so no $D010 MSB handling needed.
        var spriteGenerator = new C64SpriteGenerator(_c64);
        var singleShape = Sparse ? CreateSparseSingleColorShape() : CreateFullSingleColorShape();
        var multiShape = Sparse ? CreateSparseMultiColorShape() : CreateFullMultiColorShape();
        // spriteNumber: (x, y, doubleWidth, doubleHeight, multiColor)
        var sprites = new (byte x, byte y, bool dw, bool dh, bool mc)[]
        {
            (40,  70, false, false, false), // single, no expand
            (110, 70, true,  false, false), // single, expand X
            (180, 70, false, true,  false), // single, expand Y
            (230, 70, true,  true,  false), // single, expand X+Y
            (40,  150, false, false, true), // multi,  no expand
            (110, 150, true,  false, true), // multi,  expand X
            (180, 150, false, true,  true), // multi,  expand Y
            (230, 150, true,  true,  true), // multi,  expand X+Y
        };
        for (byte i = 0; i < NumberOfSprites; i++)
        {
            var s = sprites[i];
            spriteGenerator.CreateSprite(
                spriteNumber: i,
                x: s.x,
                y: s.y,
                doubleWidth: s.dw,
                doubleHeight: s.dh,
                multiColor: s.mc,
                spriteShape: s.mc ? multiShape : singleShape,
                spritePointer: (byte)(192 + i));

            // Per-sprite foreground color ($D027..$D02E).
            _c64.WriteIOStorage((ushort)(Vic2Addr.SPRITE_0_COLOR + i), (byte)(i + 1));
        }
        // Shared sprite multicolor registers.
        _c64.WriteIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_0, 11);
        _c64.WriteIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_1, 12);

        // ---- Some background content so the (shared) text/bitmap rendering does real work ----
        var charGenerator = new C64CharGenerator(_c64);
        charGenerator.CreateCharData();
        for (byte col = 0; col < 40; col++)
            for (byte row = 0; row < 25; row++)
                charGenerator.WriteToScreen(1, col, row);

        // Dedicated rasterizer for the selected mode (its ctor sets
        // RememberVic2RegistersPerRasterLine = !PerLineSprites, matching real behavior).
        _rasterizer = new Vic2Rasterizer(_c64, perLineSprites: PerLineSprites);
        _cyclesPerFrame = _c64.Vic2.Vic2Model.CyclesPerFrame;
    }

    [Benchmark]
    public void RenderFrame()
    {
        // Advance exactly one frame in ~4-cycle (instruction-sized) steps, rendering each step.
        const ulong step = 4;
        ulong consumed = 0;
        while (consumed < _cyclesPerFrame)
        {
            var s = step < (_cyclesPerFrame - consumed) ? step : (_cyclesPerFrame - consumed);
            _c64.Vic2.AdvanceRaster(s);
            _rasterizer.OnAfterInstruction();
            consumed += s;
        }
        _rasterizer.OnEndFrame();
    }

    // Fully-filled 24x21 single-color sprite (all 21 rows non-empty) - heaviest pixel load.
    private static byte[] CreateFullSingleColorShape()
    {
        var shape = new byte[63];
        for (int i = 0; i < shape.Length; i++)
            shape[i] = 0xFF;
        return shape;
    }

    // Fully-filled 24x21 multicolor sprite: bit pairs cycle through 01/10/11 (all non-empty).
    private static byte[] CreateFullMultiColorShape()
    {
        var shape = new byte[63];
        for (int i = 0; i < shape.Length; i++)
            shape[i] = 0b01101100; // pixel pairs 01,10,11,00
        return shape;
    }

    // Sparse single-color sprite: every other row empty, filled rows partially set (~realistic).
    private static byte[] CreateSparseSingleColorShape()
    {
        var shape = new byte[63];
        for (int row = 0; row < 21; row += 2)
        {
            shape[row * 3 + 0] = 0b00011000;
            shape[row * 3 + 1] = 0b00111100;
            shape[row * 3 + 2] = 0b00011000;
        }
        return shape;
    }

    // Sparse multicolor sprite: every other row empty, filled rows partially set.
    private static byte[] CreateSparseMultiColorShape()
    {
        var shape = new byte[63];
        for (int row = 0; row < 21; row += 2)
        {
            shape[row * 3 + 0] = 0b00011011;
            shape[row * 3 + 1] = 0b01101100;
            shape[row * 3 + 2] = 0b00011000;
        }
        return shape;
    }
}
