// using System;
// using System.Collections.Generic;
// using Avalonia.Media;
// using static Highbyte.DotNet6502.Systems.Commodore64.Video.ColorMaps;

// namespace Highbyte.DotNet6502.App.Avalonia.Core.Video.Commodore64;

// /// <summary>
// /// Manages C64 color mapping for Avalonia rendering
// /// </summary>
// internal class C64AvaloniaColors
// {
//     /// <summary>
//     /// .NET System Color to Avalonia Color map for C64.
//     /// </summary>
//     public Dictionary<System.Drawing.Color, Color> SystemToAvaloniaColorMap = new();

//     /// <summary>
//     /// C64 to Avalonia Color map for C64.
//     /// </summary>
//     public Dictionary<byte, Color> C64ToAvaloniaColorMap = new();

//     public C64AvaloniaColors(string colorMapName)
//     {
//         foreach (var systemColor in GetAllSystemColors(colorMapName))
//         {
//             SystemToAvaloniaColorMap.Add(systemColor, ToAvaloniaColor(systemColor));
//         }

//         foreach (byte c64Color in Enum.GetValues<C64Colors>())
//         {
//             C64ToAvaloniaColorMap.Add(c64Color, ToAvaloniaColor(GetSystemColor(c64Color, colorMapName)));
//         }
//     }

//     private Color ToAvaloniaColor(System.Drawing.Color color)
//     {
//         return Color.FromArgb(color.A, color.R, color.G, color.B);
//     }
// }
