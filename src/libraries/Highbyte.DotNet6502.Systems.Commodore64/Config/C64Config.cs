using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Systems.Commodore64.Config;

public class C64Config
{
    public bool LoadROMs { get; set; }

    public List<ROM> ROMs { get; set; }
    public string ROMDirectory { get; set; }

    public string C64Model { get; set; }

    public string Vic2Model { get; set; }

    public TimerMode TimerMode { get; set; }

    public bool InstrumentationEnabled { get; set; }

    public bool AudioEnabled { get; set; }

    public string ColorMapName { get; set; }

    public bool KeyboardJoystickEnabled { get; set; }
    public int KeyboardJoystick { get; set; }
    public C64KeyboardJoystickMap KeyboardJoystickMap { get; set; }
    public Type? RenderProviderType { get; set; }

    public C64Config()
    {
        // Defaults
        ROMDirectory = "";
        LoadROMs = true;     // Set false for unit tests
        ROMs = new List<ROM>();

        C64Model = "C64NTSC";
        Vic2Model = "NTSC";

        ColorMapName = ColorMaps.DEFAULT_COLOR_MAP_NAME;

        AudioEnabled = false;
        KeyboardJoystickEnabled = false;
        KeyboardJoystick = 2;

        // Settings not currently changeable by user
        TimerMode = TimerMode.UpdateEachRasterLine;
        //TimerMode = TimerMode.UpdateEachInstruction;
        KeyboardJoystickMap = new C64KeyboardJoystickMap();

        InstrumentationEnabled = false;
    }

    public object Clone()
    {
        var clone = (C64Config)this.MemberwiseClone();
        clone.ROMs = ROM.Clone(ROMs);
        return clone;
    }
}

public enum TimerMode
{
    UpdateEachInstruction,
    UpdateEachRasterLine
}
