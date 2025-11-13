using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Highbyte.DotNet6502.Benchmarks.Commodore64.Render;

[SimpleJob(RuntimeMoniker.HostProcess, launchCount:1, warmupCount:1)]
[MemoryDiagnoser]
public class Vic2RasterizerPixelWriteBenchmarks
{
    private uint[] _backBuffer;
    private uint[] _sourceBuffer;
    private int _width;
    private int _height;
    private int _pixelCount;

    [GlobalSetup]
    public void Setup()
    {
        _width = 320;
        _height = 200;
        _pixelCount = _width * _height;
        _backBuffer = GC.AllocateUninitializedArray<uint>(_pixelCount, pinned: false);
        _sourceBuffer = GC.AllocateUninitializedArray<uint>(_pixelCount, pinned: false);
        for (int i = 0; i < _pixelCount; i++)
            _sourceBuffer[i] = (uint)i;
    }

    [Benchmark]
    public void PerPixel_ArrayIndex_Write()
    {
        var back = _backBuffer;
        var count = _pixelCount;
        for (int i = 0; i < count; i++)
        {
            back[i] = (uint)i;
        }
        GC.KeepAlive(back);
    }

    [Benchmark]
    public void PerPixel_UnsafeAdd_Write()
    {
        var back = _backBuffer;
        var count = _pixelCount;
        ref uint baseRef = ref MemoryMarshal.GetArrayDataReference(back);
        for (int i = 0; i < count; i++)
        {
            Unsafe.Add(ref baseRef, i) = (uint)i;
        }
        GC.KeepAlive(back);
    }

    [Benchmark]
    public void Span_CopyTo_Write()
    {
        var src = _sourceBuffer;
        var dst = _backBuffer;
        src.AsSpan(0, _pixelCount).CopyTo(dst.AsSpan(0, _pixelCount));
        GC.KeepAlive(dst);
    }

    // [GlobalCleanup]
    // public void GlobalCleanup()
    // {
    //     Console.WriteLine("// " + "GlobalCleanup");
    // }
}
