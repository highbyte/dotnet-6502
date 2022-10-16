namespace Highbyte.DotNet6502.Monitor
{
    public class MonitorConfig
    {
        public string DefaultDirectory { get; set; }

        public void Validate()
        {
            var defaultDir = Environment.ExpandEnvironmentVariables(DefaultDirectory);
            if (!Directory.Exists(defaultDir))
                throw new Exception($"Setting {nameof(DefaultDirectory)} value {defaultDir} does not contain an existing directory.");
        }
    }
}
