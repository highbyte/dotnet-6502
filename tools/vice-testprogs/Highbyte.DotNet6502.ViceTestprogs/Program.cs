// Runs VICE's VIC-II test programs (the "testprogs" repository, directory VICII) against the C64
// emulator headlessly and compares the result with the reference screenshots VICE ships with them.
//
// Each test is a PRG with a BASIC stub. It sets up its own raster interrupt, draws its result and,
// a frame later, writes its exit code to $D7FF (the testbench's "debug cartridge" register: $00 for
// success, $FF for failure). The screenshot to compare is the frame completed before that write.
// References are VICE screenshots at its default geometry (384x272 PAL, 384x247 NTSC) with the pepto
// palette and CRT emulation off; the comparison maps both images to C64 colour indices and compares
// the area both frames cover, aligned on the display window.
//
// Usage:
//   dotnet run -c Release --project tools/vice-testprogs/Highbyte.DotNet6502.ViceTestprogs --
//     --tests <path to testprogs/VICII> --suite dentest[,border,...] [--filter <substring>]
//     [--roms <dir>] [--out <dir>] [--model pal|ntsc|both] [--frames <max frames>]
//
// ROMs: --roms, else DOTNET6502_C64_ROM_DIR, else the app's default C64 ROM directory.

using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Highbyte.DotNet6502.ViceTestprogs;

public static class Program
{
    private const ushort DebugRegister = 0xD7FF;
    private const int UnknownColour = -1;

    // VICE's pepto-pal palette (the reference screenshots' palette), by C64 colour index.
    private static readonly (byte R, byte G, byte B)[] PeptoPalette =
    [
        (0x00, 0x00, 0x00), (0xFF, 0xFF, 0xFF), (0x68, 0x37, 0x2B), (0x70, 0xA4, 0xB2),
        (0x6F, 0x3D, 0x86), (0x58, 0x8D, 0x43), (0x35, 0x28, 0x79), (0xB8, 0xC7, 0x6F),
        (0x6F, 0x4F, 0x25), (0x43, 0x39, 0x00), (0x9A, 0x67, 0x59), (0x44, 0x44, 0x44),
        (0x6C, 0x6C, 0x6C), (0x9A, 0xD2, 0x84), (0x6C, 0x5E, 0xB5), (0x95, 0x95, 0x95),
    ];

    // Where the display window (X 24, raster line 51) sits in VICE's default screenshots.
    private static readonly Dictionary<string, (int Left, int Top)> ViceDisplayWindow = new()
    {
        ["PAL"] = (32, 35),
        ["NTSC"] = (32, 23),
    };

    public static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--measure")
        {
            MeasureReference(args[1]);
            return 0;
        }
        var options = Options.Parse(args);
        if (options == null)
            return 2;

        Directory.CreateDirectory(options.OutDir);
        var results = new List<TestResult>();
        foreach (var suite in options.Suites)
        {
            var suiteDir = Path.Combine(options.TestsDir, suite);
            if (!Directory.Exists(suiteDir))
            {
                Console.WriteLine($"{suite}: directory not found under {options.TestsDir}");
                continue;
            }
            foreach (var prg in Directory.GetFiles(suiteDir, "*.prg").OrderBy(p => p, StringComparer.Ordinal))
            {
                var name = Path.GetFileNameWithoutExtension(prg);
                if (name.EndsWith("_ntscold", StringComparison.Ordinal))
                    continue;   // the 6567R56A is not modelled
                var model = name.EndsWith("_ntsc", StringComparison.Ordinal) ? "NTSC" : "PAL";
                if (options.Model != "both" && !string.Equals(options.Model, model, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (options.Filter != null && !name.Contains(options.Filter, StringComparison.OrdinalIgnoreCase))
                    continue;
                var reference = Path.Combine(suiteDir, "references", Path.GetFileName(prg) + ".png");
                var result = RunOne(suite, name, prg, model, File.Exists(reference) ? reference : null, options);
                results.Add(result);
                Console.WriteLine(result.Line());
            }
        }

        WriteSummary(results, Path.Combine(options.OutDir, "results.md"));
        var failed = results.Count(r => !r.Passed);
        Console.WriteLine($"\n{results.Count} tests, {results.Count - failed} match the reference, {failed} do not. Details in {options.OutDir}");
        return failed == 0 ? 0 : 1;
    }

    private static TestResult RunOne(string suite, string name, string prgPath, string model, string? referencePath, Options options)
    {
        var c64 = BuildC64(model, options.RomDir);
        var rasterizer = (Vic2Rasterizer)c64.RenderProvider!;
        var frameWidth = rasterizer.NativeSize.Width;
        var frameHeight = rasterizer.NativeSize.Height;

        // The debug register: the test writes its exit code there when its picture is complete.
        // Hooked in every bank configuration (the C64 has 32, for the processor port and the
        // cartridge lines); the value also goes to RAM, as it would in a configuration without I/O.
        int? exitCode = null;
        var exitFrame = -1;
        var frame = 0;
        void OnDebugWrite(ushort address, byte value)
        {
            c64.RAM[address] = value;
            if (exitCode == null)
            {
                exitCode = value;
                exitFrame = frame;
            }
        }
        var currentConfiguration = c64.Mem.CurrentConfiguration;
        for (var configuration = 0; configuration < c64.Mem.NumberOfConfigurations; configuration++)
        {
            c64.Mem.SetMemoryConfiguration(configuration);
            c64.Mem.MapWriter(DebugRegister, OnDebugWrite);
        }
        c64.Mem.SetMemoryConfiguration(currentConfiguration);

        // Boot to BASIC, load the program and start it at its SYS address.
        while (!c64.HasBasicStarted() && frame < 400) { c64.ExecuteOneFrame(); frame++; }
        for (var i = 0; i < 20; i++) { c64.ExecuteOneFrame(); frame++; }
        var prg = File.ReadAllBytes(prgPath);
        BinaryLoader.Load(c64.Mem, prg, out var loadedAt, out _);
        var start = SysAddress(c64, loadedAt) ?? loadedAt;
        TurnCursorOff(c64);
        c64.CPU.PC = start;

        if (Environment.GetEnvironmentVariable("VICETEST_TRACE") is string traceLines)
            TraceRegisterWrites(c64, traceLines.Split(',').Select(int.Parse).ToHashSet(), () => frame);

        // Run until the exit code is written (the picture is then the frame completed before it)
        // or the frame budget is spent.
        var previous = Composite(rasterizer);
        var runFrames = 0;
        uint[] captured;
        while (true)
        {
            c64.ExecuteOneFrame(); frame++; runFrames++;
            var current = Composite(rasterizer);
            if (exitCode != null)
            {
                captured = previous;
                break;
            }
            if (runFrames >= options.MaxFrames)
            {
                captured = current;
                break;
            }
            previous = current;
        }

        var ourWindow = c64.Vic2.ScreenLayouts.GetLayout(Vic2ScreenLayouts.LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false).Screen.Start;
        var ourPalette = Enumerable.Range(0, 16).Select(i => ColorMaps.GetSystemColor((byte)i, c64.ColorMapName)).Select(c => ((byte)c.R, (byte)c.G, (byte)c.B)).ToArray();
        var ours = ToIndices(captured, frameWidth, frameHeight, ourPalette, exact: true);

        var suiteOut = Path.Combine(options.OutDir, suite);
        Directory.CreateDirectory(suiteOut);
        var result = new TestResult(suite, name, model, exitCode, exitFrame >= 0 ? runFrames : null, runFrames);

        if (referencePath == null)
        {
            SaveIndexed(ours, frameWidth, frameHeight, ourPalette, Path.Combine(suiteOut, name + ".ours.png"));
            result.Note = "no reference screenshot";
            result.Passed = exitCode != 0xFF;
            return result;
        }

        using var referenceImage = Image.Load<Rgba32>(referencePath);
        var refWidth = referenceImage.Width;
        var refHeight = referenceImage.Height;
        var refPixels = new uint[refWidth * refHeight];
        referenceImage.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                    refPixels[y * refWidth + x] = (uint)(row[x].R << 16 | row[x].G << 8 | row[x].B);
            }
        });
        var reference = ToIndices(refPixels, refWidth, refHeight, PeptoPalette, exact: false);
        var viceWindow = ViceDisplayWindow[model];

        // Compare over the reference's area, aligned on the display window; count what differs.
        var diff = new int[refWidth * refHeight];
        var compared = 0;
        var mismatches = 0;
        var mismatchesInWindow = 0;
        for (var ry = 0; ry < refHeight; ry++)
        {
            for (var rx = 0; rx < refWidth; rx++)
            {
                var ox = rx - viceWindow.Left + ourWindow.X;
                var oy = ry - viceWindow.Top + ourWindow.Y;
                var i = ry * refWidth + rx;
                if (ox < 0 || oy < 0 || ox >= frameWidth || oy >= frameHeight)
                {
                    diff[i] = UnknownColour;
                    continue;
                }
                compared++;
                var same = ours[oy * frameWidth + ox] == reference[i];
                diff[i] = same ? 0 : 1;
                if (!same)
                {
                    mismatches++;
                    var inWindow = rx >= viceWindow.Left && rx < viceWindow.Left + 320 && ry >= viceWindow.Top && ry < viceWindow.Top + 200;
                    if (inWindow)
                        mismatchesInWindow++;
                }
            }
        }
        result.Compared = compared;
        result.Mismatches = mismatches;
        result.MismatchesInWindow = mismatchesInWindow;
        result.Passed = mismatches == 0 && exitCode != 0xFF;

        // Side by side: reference, ours (cropped to the reference geometry), difference mask.
        var triptych = new Image<Rgba32>(refWidth * 3 + 8, refHeight);
        for (var ry = 0; ry < refHeight; ry++)
        {
            for (var rx = 0; rx < refWidth; rx++)
            {
                var i = ry * refWidth + rx;
                triptych[rx, ry] = ToRgba(reference[i], PeptoPalette);
                var ox = rx - viceWindow.Left + ourWindow.X;
                var oy = ry - viceWindow.Top + ourWindow.Y;
                var oursIndex = ox < 0 || oy < 0 || ox >= frameWidth || oy >= frameHeight ? UnknownColour : ours[oy * frameWidth + ox];
                triptych[refWidth + 4 + rx, ry] = ToRgba(oursIndex, PeptoPalette);
                triptych[2 * refWidth + 8 + rx, ry] = diff[i] switch
                {
                    1 => new Rgba32(255, 0, 0),
                    0 => new Rgba32(0, 0, 0),
                    _ => new Rgba32(40, 40, 40),
                };
            }
        }
        triptych.SaveAsPng(Path.Combine(suiteOut, name + ".png"));
        return result;
    }

    // Where the display window sits in a reference screenshot of a test that shows the usual text
    // screen: the first row and column with a white pixel (the text) and the extent of the
    // background colour (index 6) give the window's top-left and its size.
    private static void MeasureReference(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        var w = image.Width; var h = image.Height;
        var pixels = new uint[w * h];
        image.ProcessPixelRows(a => { for (var y = 0; y < a.Height; y++) { var row = a.GetRowSpan(y); for (var x = 0; x < row.Length; x++) pixels[y * w + x] = (uint)(row[x].R << 16 | row[x].G << 8 | row[x].B); } });
        var idx = ToIndices(pixels, w, h, PeptoPalette, exact: false);
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                if (idx[y * w + x] == 6) { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
        Console.WriteLine($"{Path.GetFileName(path)}: {w}x{h}, background colour spans x {minX}-{maxX} ({maxX - minX + 1} px), y {minY}-{maxY} ({maxY - minY + 1} px); corner colour index {idx[0]}");
    }

    private static C64 BuildC64(string model, string romDir)
    {
        var config = new C64Config
        {
            LoadROMs = true,
            ROMDirectory = romDir,
            C64Model = model == "NTSC" ? "C64NTSC" : "C64PAL",
            Vic2Model = model,
            AudioEnabled = false,
            RenderProviderType = typeof(Vic2Rasterizer),
            Vic2RasterizerPerLineSprites = true,
            ROMs =
            [
                new ROM { Name = "kernal", File = "kernal.901227-03.bin" },
                new ROM { Name = "basic", File = "basic.901226-01.bin" },
                new ROM { Name = "chargen", File = "characters.901225-01.bin" },
            ],
        };
        return C64.BuildC64(config, NullLoggerFactory.Instance);
    }

    // What the KERNAL does when a program starts: stop the cursor blinking and, if the cursor is
    // shown right now (the character under it inverted), put the character back. Otherwise the
    // blink phase at the moment the program is started decides whether one cell differs from the
    // reference, which was taken from a program started by VICE's autostart.
    private static void TurnCursorOff(C64 c64)
    {
        const ushort BlinkSwitch = 0xCC, CharacterUnderCursor = 0xCE, CursorShown = 0xCF, CursorLinePointer = 0xD1, CursorColumn = 0xD3;
        if (c64.Mem[CursorShown] != 0)
        {
            var cursorAddress = (ushort)(c64.Mem[CursorLinePointer] | c64.Mem[(ushort)(CursorLinePointer + 1)] << 8);
            c64.Mem[(ushort)(cursorAddress + c64.Mem[CursorColumn])] = c64.Mem[CharacterUnderCursor];
            c64.Mem[CursorShown] = 0;
        }
        c64.Mem[BlinkSwitch] = 1;
    }

    // Diagnostics: every VIC-II register write on the given raster lines, with its cycle.
    private static void TraceRegisterWrites(C64 c64, HashSet<int> lines, Func<int> frame)
    {
        var cyclesPerLine = (int)c64.Vic2.Vic2Model.CyclesPerLine;
        var inner = c64.Vic2.RegisterWriteObserver;
        c64.Vic2.RegisterWriteObserver = (frameCycle, register, value) =>
        {
            inner?.Invoke(frameCycle, register, value);
            var line = (int)(frameCycle / (ulong)cyclesPerLine);
            if (lines.Contains(line))
                Console.WriteLine($"  trace: frame {frame()} line {line} cycle {frameCycle % (ulong)cyclesPerLine}: ${register:X4} = ${value:X2}");
        };
    }

    // The address in the BASIC stub's SYS statement: link (2), line number (2), token $9E, digits.
    private static ushort? SysAddress(C64 c64, ushort basicStart)
    {
        var p = (ushort)(basicStart + 4);
        if (c64.Mem[p] != 0x9E)
            return null;
        p++;
        var value = 0;
        var digits = 0;
        while (true)
        {
            var b = c64.Mem[p++];
            if (b == ' ')
                continue;
            if (b < '0' || b > '9')
                break;
            value = value * 10 + (b - '0');
            digits++;
        }
        return digits > 0 ? (ushort)value : null;
    }

    private static uint[] Composite(Vic2Rasterizer rasterizer)
    {
        var layers = rasterizer.CurrentFrontLayerBuffers;
        var result = layers[0].ToArray();
        for (var l = 1; l < layers.Count; l++)
        {
            var layer = layers[l].Span;
            for (var p = 0; p < result.Length; p++)
                if ((layer[p] & 0xFF000000) != 0)
                    result[p] = layer[p];
        }
        return result;
    }

    // Pixels to C64 colour indices: exact match against the emulator's own palette, nearest match
    // (with a generous tolerance) against the reference's palette.
    private static int[] ToIndices(uint[] pixels, int width, int height, (byte R, byte G, byte B)[] palette, bool exact)
    {
        var indices = new int[width * height];
        var cache = new Dictionary<uint, int>();
        for (var i = 0; i < indices.Length; i++)
        {
            var rgb = pixels[i] & 0xFFFFFF;
            if (!cache.TryGetValue(rgb, out var index))
            {
                index = Nearest(rgb, palette, exact);
                cache[rgb] = index;
            }
            indices[i] = index;
        }
        return indices;
    }

    private static int Nearest(uint rgb, (byte R, byte G, byte B)[] palette, bool exact)
    {
        var r = (int)(rgb >> 16) & 0xFF;
        var g = (int)(rgb >> 8) & 0xFF;
        var b = (int)rgb & 0xFF;
        var best = UnknownColour;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < palette.Length; i++)
        {
            var d = (r - palette[i].R) * (r - palette[i].R) + (g - palette[i].G) * (g - palette[i].G) + (b - palette[i].B) * (b - palette[i].B);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = i;
            }
        }
        return exact && bestDistance != 0 ? UnknownColour : best;
    }

    private static Rgba32 ToRgba(int index, (byte R, byte G, byte B)[] palette)
        => index < 0 ? new Rgba32(255, 0, 255) : new Rgba32(palette[index].R, palette[index].G, palette[index].B);

    private static void SaveIndexed(int[] indices, int width, int height, (byte R, byte G, byte B)[] palette, string path)
    {
        using var image = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                image[x, y] = ToRgba(indices[y * width + x], palette);
        image.SaveAsPng(path);
    }

    private static void WriteSummary(List<TestResult> results, string path)
    {
        var lines = new List<string>
        {
            "| Suite | Test | Model | Exit | Frames | Compared | Mismatch | In window | Result |",
            "|---|---|---|---|---|---|---|---|---|",
        };
        lines.AddRange(results.Select(r => r.Row()));
        File.WriteAllLines(path, lines);
    }

    private sealed class TestResult(string suite, string name, string model, int? exitCode, int? exitAfterFrames, int framesRun)
    {
        public int Compared { get; set; }
        public int Mismatches { get; set; }
        public int MismatchesInWindow { get; set; }
        public bool Passed { get; set; }
        public string? Note { get; set; }

        private string Exit => exitCode == null ? "none" : $"${exitCode:X2}";
        private string Frames => exitAfterFrames?.ToString() ?? $"{framesRun} (timeout)";

        public string Line()
            => $"{suite}/{name} [{model}] exit {Exit} after {Frames} frames: {(Note ?? $"{Mismatches} of {Compared} pixels differ ({MismatchesInWindow} in the display window)")} -> {(Passed ? "MATCH" : "DIFF")}";

        public string Row()
            => $"| {suite} | {name} | {model} | {Exit} | {Frames} | {Compared} | {Mismatches} | {MismatchesInWindow} | {(Passed ? "match" : "differs")}{(Note == null ? "" : $" ({Note})")} |";
    }

    private sealed class Options
    {
        public string TestsDir { get; private set; } = "";
        public List<string> Suites { get; } = [];
        public string? Filter { get; private set; }
        public string RomDir { get; private set; } = "";
        public string OutDir { get; private set; } = "vice-testprogs-results";
        public string Model { get; private set; } = "both";
        public int MaxFrames { get; private set; } = 600;

        public static Options? Parse(string[] args)
        {
            var o = new Options();
            for (var i = 0; i < args.Length; i++)
            {
                string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
                switch (args[i])
                {
                    case "--tests": o.TestsDir = Next(); break;
                    case "--suite": o.Suites.AddRange(Next().Split(',', StringSplitOptions.RemoveEmptyEntries)); break;
                    case "--filter": o.Filter = Next(); break;
                    case "--roms": o.RomDir = Next(); break;
                    case "--out": o.OutDir = Next(); break;
                    case "--model": o.Model = Next().ToLowerInvariant() switch { "pal" => "PAL", "ntsc" => "NTSC", _ => "both" }; break;
                    case "--frames": o.MaxFrames = int.Parse(Next()); break;
                    default:
                        Console.WriteLine($"Unknown argument {args[i]}");
                        return null;
                }
            }
            if (o.TestsDir == "" || o.Suites.Count == 0)
            {
                Console.WriteLine("Usage: --tests <path to testprogs/VICII> --suite <name>[,<name>...] [--filter <substring>] [--roms <dir>] [--out <dir>] [--model pal|ntsc|both] [--frames <max>]");
                return null;
            }
            if (o.RomDir == "")
                o.RomDir = Environment.GetEnvironmentVariable("DOTNET6502_C64_ROM_DIR") ?? C64SystemConfig.DefaultROMDirectory;
            return o;
        }
    }
}
