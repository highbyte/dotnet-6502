using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Monitor
{
    public class MonitorConfig
    {
        public string DefaultDirectory { get; set; }

        public void Validate()
        {
            if (string.IsNullOrEmpty(DefaultDirectory))
                DefaultDirectory = Environment.CurrentDirectory;
            var defaultDir = PathHelper.ExpandOSEnvironmentVariables(DefaultDirectory);
            if (!Directory.Exists(defaultDir))
                throw new Exception($"Setting {nameof(DefaultDirectory)} value {defaultDir} does not contain an existing directory.");
        }
    }
}
