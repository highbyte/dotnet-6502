// using System;
// using System.Collections.Generic;
// using System.Runtime.InteropServices;
// using Avalonia;
// using Avalonia.Media.Imaging;
// using Avalonia.Platform;
// using Highbyte.DotNet6502.Systems.Commodore64;
// using Highbyte.DotNet6502.Systems.Commodore64.Render;

// namespace Highbyte.DotNet6502.App.Avalonia.Core.Video.Commodore64;

// /// <summary>
// /// Renders a C64 system using Avalonia's WriteableBitmap for cross-platform compatibility.
// /// This renderer works efficiently on all Avalonia targets including WebAssembly.
// /// 
// /// Uses the common C64RenderBase base class for rendering logic, and implements
// /// the platform-specific rendering using WriteableBitmap for optimal performance.
// /// </summary>
// public class C64AvaloniaRenderer : C64RenderBase, IAvaloniaBitmapRenderer
// {
//     private WriteableBitmap? _writeableBitmap;
//     private readonly C64AvaloniaColors _c64AvaloniaColors;
//     private readonly Dictionary<byte, uint> _c64ToRenderColorMap;
//     private Action? _newFrameHasBeenDrawnCallback;

//     protected override Dictionary<byte, uint> C64ToRenderColorMap => _c64ToRenderColorMap;
//     protected override bool FlipY => false;
//     protected override uint TransparentColor => 0x00000000; // Transparent black
//     protected override string StatsCategory => "Avalonia-WriteableBitmap";

//     public WriteableBitmap? Bitmap => _writeableBitmap;

//     public C64AvaloniaRenderer(C64 c64) : base(c64)
//     {
//         _c64AvaloniaColors = new C64AvaloniaColors(c64.ColorMapName);
//         _c64ToRenderColorMap = new Dictionary<byte, uint>();
//     }

//     protected override void OnBeforeInit()
//     {
//         // Convert C64 colors to uint format (ARGB)
//         foreach (var colorPair in _c64AvaloniaColors.C64ToAvaloniaColorMap)
//         {
//             _c64ToRenderColorMap[colorPair.Key] = colorPair.Value.ToUInt32();
//         }
//     }

//     protected override void OnAfterInit()
//     {
//         InitWriteableBitmap();
//     }

//     protected override void OnCleanup()
//     {
//         _writeableBitmap?.Dispose();
//         _writeableBitmap = null;
//     }

//     protected override void RenderArrays()
//     {
//         if (_writeableBitmap == null) return;

//         // Copy pixel data to WriteableBitmap
//         using var frameBuffer = _writeableBitmap.Lock();

//         var width = C64.Vic2.Vic2Screen.VisibleWidth;
//         var height = C64.Vic2.Vic2Screen.VisibleHeight;

//         // Create a combined pixel array for the final image
//         var finalPixels = new uint[width * height];

//         // First, copy background and border pixels
//         Array.Copy(PixelArray_BackgroundAndBorder, finalPixels, finalPixels.Length);

//         // Then, blend foreground pixels (text, bitmaps, high-priority sprites)
//         // Only overwrite background pixels if foreground pixel is not transparent
//         for (int i = 0; i < finalPixels.Length; i++)
//         {
//             var foregroundPixel = PixelArray_Foreground[i];
//             if (foregroundPixel != TransparentColor)
//             {
//                 finalPixels[i] = foregroundPixel;
//             }
//         }

//         // Most efficient approach: Copy the entire uint array as bytes in one operation
//         // Use MemoryMarshal.AsBytes to view uint[] as Span<byte> without allocation
//         var pixelBytesSpan = MemoryMarshal.AsBytes(finalPixels.AsSpan());

//         // Check if the bitmap stride matches our data stride
//         var ourBytesPerRow = width * 4;
//         if (frameBuffer.RowBytes == ourBytesPerRow)
//         {
//             // Perfect case: we can copy the entire image in one operation
//             Marshal.Copy(pixelBytesSpan.ToArray(), 0, frameBuffer.Address, pixelBytesSpan.Length);
//         }
//         else
//         {
//             // Row stride doesn't match (bitmap has padding), copy row by row
//             for (int y = 0; y < height; y++)
//             {
//                 var sourceOffset = y * ourBytesPerRow;
//                 var targetPtr = IntPtr.Add(frameBuffer.Address, y * frameBuffer.RowBytes);

//                 // Copy just this row
//                 var rowSpan = pixelBytesSpan.Slice(sourceOffset, ourBytesPerRow);
//                 Marshal.Copy(rowSpan.ToArray(), 0, targetPtr, ourBytesPerRow);
//             }
//         }
//     }

//     protected override void OnAfterGenerateFrame()
//     {
//         // Notify that a new frame has been drawn (useful for FPS tracking)
//         _newFrameHasBeenDrawnCallback?.Invoke();
//     }

//     public void SetNewFrameHasBeenDrawnCallback(Action? newFrameHasBeenDrawnCallback)
//     {
//         _newFrameHasBeenDrawnCallback = newFrameHasBeenDrawnCallback;
//     }

//     private void InitWriteableBitmap()
//     {
//         var width = C64.Vic2.Vic2Screen.VisibleWidth;
//         var height = C64.Vic2.Vic2Screen.VisibleHeight;

//         // Create WriteableBitmap with BGRA8888 format for optimal performance
//         _writeableBitmap = new WriteableBitmap(
//             new PixelSize(width, height),
//             new Vector(96, 96),
//             PixelFormat.Bgra8888,
//             AlphaFormat.Premul);
//     }
// }
