// using System;
// using Highbyte.DotNet6502.Impl.Skia.Commodore64.Video.v3;
// using Highbyte.DotNet6502.Systems.Commodore64;

// namespace Highbyte.DotNet6502.App.Avalonia.Core.Video.Commodore64;

// /// <summary>
// /// </summary>
// public class C64AvaloniaSkiaRenderer : C64SkiaRenderer3, IAvaloniaDrawFrameRenderer
// {
//     private Action? _newFrameHasBeenDrawnCallback;

//     public C64AvaloniaSkiaRenderer(C64 c64) : base(c64, null)
//     {
//     }

//     public void SetNewFrameHasBeenDrawnCallback(Action? newFrameHasBeenDrawnCallback)
//     {
//         _newFrameHasBeenDrawnCallback = newFrameHasBeenDrawnCallback;
//     }

//     protected override void OnAfterGenerateFrame()
//     {
//         // Notify that a new frame has been drawn (useful for FPS tracking)
//         _newFrameHasBeenDrawnCallback?.Invoke();
//     }
// }
