namespace Highbyte.DotNet6502.Systems.Commodore64.Audio;

/// <summary>
/// </summary>
public class Sid
{
    private readonly InternalSidState _internalSidState;
    public InternalSidState InternalSidState => _internalSidState;

    private Sid(C64 c64)
    {
        _internalSidState = new InternalSidState(c64);
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    public static Sid BuildSid(C64 c64)
    {
        var sid = new Sid(c64);
        return sid;
    }

    public void MapIOLocations(Memory c64Mem)
    {
        // Note: Most SID registers are write-only.

        // Voice 1 registers
        c64Mem.MapReader(SidAddr.FRELO1, (_) => 0);
        c64Mem.MapWriter(SidAddr.FRELO1, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.FREHI1, (_) => 0);
        c64Mem.MapWriter(SidAddr.FREHI1, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.PWLO1, (_) => 0);
        c64Mem.MapWriter(SidAddr.PWLO1, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.PWHI1, (_) => 0);
        c64Mem.MapWriter(SidAddr.PWHI1, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.VCREG1, (_) => 0);
        c64Mem.MapWriter(SidAddr.VCREG1, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.ATDCY1, (_) => 0);
        c64Mem.MapWriter(SidAddr.ATDCY1, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.SUREL1, (_) => 0);
        c64Mem.MapWriter(SidAddr.SUREL1, InternalSidState.SetSidRegValue);


        // Voice 2 registers
        c64Mem.MapReader(SidAddr.FRELO2, (_) => 0);
        c64Mem.MapWriter(SidAddr.FRELO2, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.FREHI2, (_) => 0);
        c64Mem.MapWriter(SidAddr.FREHI2, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.PWLO2, (_) => 0);
        c64Mem.MapWriter(SidAddr.PWLO2, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.PWHI2, (_) => 0);
        c64Mem.MapWriter(SidAddr.PWHI2, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.VCREG2, (_) => 0);
        c64Mem.MapWriter(SidAddr.VCREG2, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.ATDCY2, (_) => 0);
        c64Mem.MapWriter(SidAddr.ATDCY2, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.SUREL2, (_) => 0);
        c64Mem.MapWriter(SidAddr.SUREL2, InternalSidState.SetSidRegValue);


        // Voice 3 registers
        c64Mem.MapReader(SidAddr.FRELO3, (_) => 0);
        c64Mem.MapWriter(SidAddr.FRELO3, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.FREHI3, (_) => 0);
        c64Mem.MapWriter(SidAddr.FREHI3, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.PWLO3, (_) => 0);
        c64Mem.MapWriter(SidAddr.PWLO3, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.PWHI3, (_) => 0);
        c64Mem.MapWriter(SidAddr.PWHI3, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.VCREG3, (_) => 0);
        c64Mem.MapWriter(SidAddr.VCREG3, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.ATDCY3, (_) => 0);
        c64Mem.MapWriter(SidAddr.ATDCY3, InternalSidState.SetSidRegValue);

        c64Mem.MapReader(SidAddr.SUREL3, (_) => 0);
        c64Mem.MapWriter(SidAddr.SUREL3, InternalSidState.SetSidRegValue);


        // Common audio registers
        c64Mem.MapReader(SidAddr.SIGVOL, (_) => 0);
        c64Mem.MapWriter(SidAddr.SIGVOL, InternalSidState.SetSidRegValue);
    }

    /// <summary>
    /// Lookup table that maps SID attack duration value (0-15) to milliseconds
    /// </summary>
    public static Dictionary<int, int> AttackDurationMs = new()
    {
        {0, 2},
        {1, 8},
        {2, 16},
        {3, 24},
        {4, 38},
        {5, 56},
        {6, 68},
        {7, 80},
        {8, 100},
        {9, 250},
        {10, 500},
        {11, 800},
        {12, 1000},
        {13, 3000},
        {14, 5000},
        {15, 8000},
    };

    /// <summary>
    /// Lookup table that maps SID decay duration value (0-15) to milliseconds
    /// </summary>
    public static Dictionary<int, int> DecayDurationMs = new()
    {
        {0, 6},
        {1, 24},
        {2, 48},
        {3, 72},
        {4, 114},
        {5, 168},
        {6, 204},
        {7, 240},
        {8, 300},
        {9, 750},
        {10, 1500},
        {11, 2400},
        {12, 3000},
        {13, 9000},
        {14, 15000},
        {15, 24000},
    };

    /// <summary>
    /// Lookup table that maps SID release duration value (0-15) to milliseconds
    /// </summary>
    public static Dictionary<int, int> ReleaseDurationMs = new()
    {
        {0, 6},
        {1, 24},
        {2, 48},
        {3, 72},
        {4, 114},
        {5, 168},
        {6, 204},
        {7, 240},
        {8, 300},
        {9, 750},
        {10, 1500},
        {11, 2400},
        {12, 3000},
        {13, 9000},
        {14, 15000},
        {15, 24000},
    };
}
