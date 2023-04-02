namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// </summary>
public class Sid
{

    private readonly InternalSidState _sidSoundData = new InternalSidState();
    public InternalSidState InternalSidState => _sidSoundData;

    /// <summary>
    /// </summary>
    /// <returns></returns>
    public static Sid BuildSid()
    {
        var sid = new Sid();
        return sid;
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
