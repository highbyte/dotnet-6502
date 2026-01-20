using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using SadConsole;
using SadConsole.Components;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.App.SadConsole;

/// <summary>
/// Helper class for loading embedded resources (fonts, images) in SadConsole
/// </summary>
public static class EmbeddedResourceHelper
{
    private static readonly Assembly s_assembly = typeof(EmbeddedResourceHelper).Assembly;

    /// <summary>
    /// Loads a font from embedded resources
    /// </summary>
    /// <param name="fontResourcePath">Path like "Fonts/C64_ROM.font"</param>
    /// <param name="imageResourcePath">Path like "Fonts/c64_chargen_unshifted_dump.png"</param>
    /// <returns>The loaded font</returns>
    public static IFont LoadFontFromEmbeddedResource(string fontResourcePath, string? imageResourcePath = null)
    {
        // Convert path to resource name format
        string fontResourceName = GetResourceName(fontResourcePath);
        
        // Load the font config (.font file) from embedded resources
        using var fontStream = s_assembly.GetManifestResourceStream(fontResourceName);
        if (fontStream is null)
            throw new FileNotFoundException($"Embedded resource not found: {fontResourceName}");

        // Read font configuration
        string fontConfig;
        using (var reader = new StreamReader(fontStream))
        {
            fontConfig = reader.ReadToEnd();
        }

        // Parse the font config to get the image file name if not provided
        if (string.IsNullOrEmpty(imageResourcePath))
        {
            imageResourcePath = ExtractImagePathFromFontConfig(fontConfig, fontResourcePath);
        }

        string imageResourceName = GetResourceName(imageResourcePath);
        
        // Load the image from embedded resources
        using var imageStream = s_assembly.GetManifestResourceStream(imageResourceName);
        if (imageStream is null)
            throw new FileNotFoundException($"Embedded resource not found: {imageResourceName}");

        // Create texture from stream
        var texture = GameHost.Instance.GetTexture(imageStream);

        // Parse the font configuration and create font
        var font = ParseFontConfig(fontConfig, texture, fontResourcePath);
        
        return font;
    }

    /// <summary>
    /// Loads an image texture from embedded resources
    /// </summary>
    public static ITexture LoadTextureFromEmbeddedResource(string resourcePath)
    {
        string resourceName = GetResourceName(resourcePath);
        
        using var stream = s_assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new FileNotFoundException($"Embedded resource not found: {resourceName}");

        return GameHost.Instance.GetTexture(stream);
    }

    /// <summary>
    /// Creates a DrawImage component from an embedded resource
    /// </summary>
    public static DrawImage CreateDrawImageFromEmbeddedResource(string resourcePath)
    {
        var texture = LoadTextureFromEmbeddedResource(resourcePath);
        // DrawImage requires a Texture2D (MonoGame) - cast the ITexture to get the underlying texture
        var gameTexture = (global::SadConsole.Host.GameTexture)texture;
        return new DrawImage(gameTexture.Texture);
    }

    private static string GetResourceName(string path)
    {
        // Convert file path to embedded resource name
        // e.g., "Fonts/C64_ROM.font" -> "Highbyte.DotNet6502.App.SadConsole.Fonts.C64_ROM.font"
        var resourceName = path.Replace('/', '.').Replace('\\', '.');
        return $"{s_assembly.GetName().Name}.{resourceName}";
    }

    private static string ExtractImagePathFromFontConfig(string fontConfig, string fontPath)
    {
        // Parse the .font JSON config to extract the image file name
        // The .font file is JSON format with a "FilePath" property
        var lines = fontConfig.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Contains("\"FilePath\""))
            {
                // Extract the file path value
                var start = line.IndexOf(":") + 1;
                var value = line.Substring(start).Trim().Trim('"', ',', ' ');
                
                // Combine with the font's directory
                var fontDir = Path.GetDirectoryName(fontPath)?.Replace('\\', '/') ?? "";
                return string.IsNullOrEmpty(fontDir) ? value : $"{fontDir}/{value}";
            }
        }
        throw new InvalidOperationException($"Could not find FilePath in font config: {fontPath}");
    }

    private static IFont ParseFontConfig(string fontConfig, ITexture texture, string fontName)
    {
        // Parse the JSON font configuration
        // Simple parsing for the key properties
        int columns = 16;  // default
        int rows = 16;     // default
        int glyphWidth = 8;
        int glyphHeight = 16;
        int glyphPadding = 0;
        int solidGlyphIndex = 219;

        var lines = fontConfig.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.Contains("\"Columns\""))
                columns = ExtractIntValue(trimmedLine);
            else if (trimmedLine.Contains("\"Rows\""))
                rows = ExtractIntValue(trimmedLine);
            else if (trimmedLine.Contains("\"GlyphWidth\""))
                glyphWidth = ExtractIntValue(trimmedLine);
            else if (trimmedLine.Contains("\"GlyphHeight\""))
                glyphHeight = ExtractIntValue(trimmedLine);
            else if (trimmedLine.Contains("\"GlyphPadding\""))
                glyphPadding = ExtractIntValue(trimmedLine);
            else if (trimmedLine.Contains("\"SolidGlyphIndex\""))
                solidGlyphIndex = ExtractIntValue(trimmedLine);
        }

        // Create the font using SadFont constructor
        // SadFont(glyphWidth, glyphHeight, glyphPadding, rows, columns, solidGlyphIndex, image, name)
        return new SadFont(
            glyphWidth,
            glyphHeight,
            glyphPadding,
            rows,
            columns,
            solidGlyphIndex,
            texture,
            fontName
        );
    }

    private static int ExtractIntValue(string line)
    {
        var start = line.IndexOf(":") + 1;
        var value = line.Substring(start).Trim().Trim(',', ' ');
        return int.Parse(value);
    }
}
