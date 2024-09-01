using System.Reflection;
using Silk.NET.Core;

namespace Highbyte.DotNet6502.App.SilkNetNative;
public class SilkNetImageLoader
{
    public static RawImage ReadFileAsRawImage(string path, bool isEmbeddedResource = false)
    {
        byte[] fileBytes;
        if (isEmbeddedResource)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream? resourceStream = assembly.GetManifestResourceStream(path))
            {
                if (resourceStream == null)
                    throw new Exception($"Cannot open stream to resource {path} in current assembly.");
                using (MemoryStream ms = new MemoryStream())
                {
                    resourceStream.CopyTo(ms);
                    fileBytes = ms.ToArray();
                }
            }
        }
        else
        {
            fileBytes = File.ReadAllBytes(path);
        }

        var bitmap = SKBitmap.Decode(fileBytes);
        byte[] bytes = new byte[bitmap.Pixels.Length * 4];
        int index = 0;
        foreach (var pixel in bitmap.Pixels)
        {
            bytes[index++] = pixel.Red;
            bytes[index++] = pixel.Green;
            bytes[index++] = pixel.Blue;
            bytes[index++] = pixel.Alpha;
        }
        var rawImage = new RawImage(bitmap.Width, bitmap.Height, new Memory<byte>(bytes));
        return rawImage;
    }
}
