namespace Highbyte.DotNet6502.Monitor;

public class MonitorConfig
{
    public string? DefaultDirectory { get; set; }

    public int? MaxLineLength { get; set; }

    public bool StopAfterBRKInstruction { get; set; }
    public bool StopAfterUnknownInstruction { get; set; }


    public MonitorConfig()
    {
        MaxLineLength = null;
        StopAfterBRKInstruction = false;
        StopAfterUnknownInstruction = false;
    }

    public void Validate()
    {
        if (string.IsNullOrEmpty(DefaultDirectory))
            DefaultDirectory = Environment.CurrentDirectory;
        var defaultDir = PathHelper.ExpandOSEnvironmentVariables(DefaultDirectory);
        if (!Directory.Exists(defaultDir))
            throw new DotNet6502Exception($"Setting {nameof(DefaultDirectory)} value {defaultDir} does not contain an existing directory.");
    }
}
