namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// C64 SID chip audio related addresses: 0xd400 0xd41c
/// Ref: https://github.com/mist64/c64ref/blob/master/Source/c64io/c64io_mapc64.txt
/// </summary>
public class SidAddr
{
    // Voice 1 registers
    public const ushort FRELO1 = 0xd400;    // 54272
    public const ushort FREHI1 = 0xd401;    // 54273
    public const ushort PWLO1 =  0xd402;    // 54274
    public const ushort PWHI1 =  0xd403;    // 54275
    public const ushort VCREG1 = 0xd404;    // 54276
    public const ushort ATDCY1 = 0xd405;    // 54277
    public const ushort SUREL1 = 0xd406;    // 54278

    // Voice 2 registers
    public const ushort FRELO2 = 0xd407;    // 54279
    public const ushort FREHI2 = 0xd408;    // 54280
    public const ushort PWLO2 =  0xd409;    // 54281
    public const ushort PWHI2 =  0xd40a;    // 54282
    public const ushort VCREG2 = 0xd40b;    // 54283
    public const ushort ATDCY2 = 0xd40c;    // 54284
    public const ushort SUREL2 = 0xd40d;    // 54285

    // Voice 3 registers
    public const ushort FRELO3 = 0xd40e;    // 54286
    public const ushort FREHI3 = 0xd40f;    // 54287
    public const ushort PWLO3 =  0xd410;    // 54288
    public const ushort PWHI3 =  0xd411;    // 54289
    public const ushort VCREG3 = 0xd412;    // 54290
    public const ushort ATDCY3 = 0xd413;    // 54291
    public const ushort SUREL3 = 0xd414;    // 54292

    // Common audio registers
    public const ushort CUTLO =  0xd415;    // 54293
    public const ushort CUTHI =  0xd416;    // 54294
    public const ushort RESON =  0xd417;    // 54295
    public const ushort SIGVOL = 0xd418;    // 54296


    /// <summary>
    /// Map a voice register "typ" and it's voice number to an actual Sid address.
    /// Note: This does not contain all sid registers, only the ones that are per voice.
    /// </summary>
    public static Dictionary<string, ushort> VoiceRegisterMap = new()
    {
        // Voice 1
        { $"{SidVoiceRegisterType.FRELO}1", FRELO1 },
        { $"{SidVoiceRegisterType.FREHI}1", FREHI1 },
        { $"{SidVoiceRegisterType.PWLO}1",  PWLO1 },
        { $"{SidVoiceRegisterType.PWHI}1",  PWHI1 },
        { $"{SidVoiceRegisterType.VCREG}1", VCREG1 },
        { $"{SidVoiceRegisterType.ATDCY}1", ATDCY1 },
        { $"{SidVoiceRegisterType.SUREL}1", SUREL1 },

        // Voice 2
        { $"{SidVoiceRegisterType.FRELO}2", FRELO2 },
        { $"{SidVoiceRegisterType.FREHI}2", FREHI2 },
        { $"{SidVoiceRegisterType.PWLO}2",  PWLO2 },
        { $"{SidVoiceRegisterType.PWHI}2",  PWHI2 },
        { $"{SidVoiceRegisterType.VCREG}2", VCREG2 },
        { $"{SidVoiceRegisterType.ATDCY}2", ATDCY2 },
        { $"{SidVoiceRegisterType.SUREL}2", SUREL2 },

        // Voice 3
        { $"{SidVoiceRegisterType.FRELO}3", SidAddr.FRELO3 },
        { $"{SidVoiceRegisterType.FREHI}3", SidAddr.FREHI3 },
        { $"{SidVoiceRegisterType.PWLO}3",  SidAddr.PWLO3 },
        { $"{SidVoiceRegisterType.PWHI}3",  SidAddr.PWHI3 },
        { $"{SidVoiceRegisterType.VCREG}3", SidAddr.VCREG3 },
        { $"{SidVoiceRegisterType.ATDCY}3", SidAddr.ATDCY3 },
        { $"{SidVoiceRegisterType.SUREL}3", SidAddr.SUREL3 },
    };
}

public enum SidVoiceRegisterType
{
    FRELO,
    FREHI,
    PWLO,
    PWHI,
    VCREG,
    ATDCY,
    SUREL
}

