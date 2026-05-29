namespace Highbyte.DotNet6502.Systems.Vic20.Video;

/// <summary>
/// Converts VIC-20 screen codes to printable strings suitable for the C64 Pro Mono font.
///
/// The VIC-20 uses the same screen-code encoding as the C64 (PETSCII-based):
///   screen code 0x00-0x1F  → PETSCII 0x40-0x5F  (uppercase alphabet, @, etc.)
///   screen code 0x20-0x3F  → PETSCII 0x20-0x3F  (space, digits, common symbols)
///   screen code 0x40-0x5F  → PETSCII 0xC0-0xDF  (inverted/special graphics)
///   etc.
///
/// The conversion logic is identical to Petscii.C64ScreenCodeToPetscII in the C64
/// library. It lives here to avoid a cross-library dependency.
/// </summary>
public static class Vic20ScreenCode
{
    public static string ScreenCodeToUnicode(byte screenCode)
    {
        // Special-case: inverted space (screen code 0xA0, 0xE0)
        if (screenCode == 0xA0 || screenCode == 0xE0)
            return "█"; // full-block glyph

        var petsciiCode = ScreenCodeToPetscii(screenCode);
        var asciiCode   = PetsciiToAscii(petsciiCode);

        if (asciiCode == 0x00)
            return " "; // uninitialized / null → show as space

        // Lowercase a-z in the ASCII result → display as uppercase (C64/VIC-20 look)
        if (asciiCode >= 0x61 && asciiCode <= 0x7A)
            return ((char)(asciiCode - 0x20)).ToString();

        return ((char)asciiCode).ToString();
    }

    // Same formula as Petscii.C64ScreenCodeToPetscII (http://sta.c64.org/cbm64scrtopet.html)
    private static byte ScreenCodeToPetscii(byte sc) => sc switch
    {
        >= 0   and <= 31  => (byte)(sc + 64),
        >= 32  and <= 63  => sc,
        >= 64  and <= 93  => (byte)(sc + 128),
        94                => 255,
        95                => 223,
        >= 96  and <= 127 => (byte)(sc + 64),
        >= 128 and <= 159 => (byte)(sc - 128),
        >= 160 and <= 191 => (byte)(sc - 128),
        >= 192 and <= 223 => (byte)(sc - 64),
        _                 => (byte)(sc - 64),
    };

    // Same formula as Petscii.PetscIIToAscII
    private static byte PetsciiToAscii(byte p)
    {
        if (p >= 97  && p <= 122) return (byte)(p - 32); // PETSCII a-z → ASCII A-Z
        if (p >= 65  && p <= 90)  return (byte)(p + 32); // PETSCII A-Z → ASCII a-z
        if (p >= 192 && p <= 223)
        {
            var a = (byte)(p - 96);
            return (a >= 97 && a <= 122) ? (byte)(a - 32) : a;
        }
        return p;
    }
}
