using System.Diagnostics;

namespace Highbyte.DotNet6502.Systems.Generic.Config
{
    public class GenericComputerConfig
    {
        public const string ConfigSectionName = "Highbyte.DotNet6502.GenericComputer";
        public string ProgramBinaryFile { get; set; }
        public byte[] ProgramBinary { get; set; }
        public bool StopAtBRK { get; set; }

        public ulong CPUCyclesPerFrame { get; set; }
        public float ScreenRefreshFrequencyHz { get; set; }
        public EmulatorMemoryConfig Memory { get; set; }

        public GenericComputerConfig()
        {
            Memory = new();
            StopAtBRK = true;
            CPUCyclesPerFrame = 5000;
            ScreenRefreshFrequencyHz = 60.0f;
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(ProgramBinaryFile) && ProgramBinary?.Length == 0)
                throw new Exception($"Setting {nameof(ProgramBinaryFile)} or {nameof(ProgramBinary)} must be specified.");
            if (!string.IsNullOrEmpty(ProgramBinaryFile) && (ProgramBinary != null & ProgramBinary?.Length > 0))
                throw new Exception($"Setting {nameof(ProgramBinaryFile)} and {nameof(ProgramBinary)} cannot both be specified.");

            if (!string.IsNullOrEmpty(ProgramBinaryFile))
            {
                var prgFile = Environment.ExpandEnvironmentVariables(ProgramBinaryFile);

                if (!File.Exists(prgFile))
                {
                    Debug.WriteLine($"File does not exist.");
                    throw new Exception($"Cannot find 6502 binary file: {prgFile}");
                }
            }

            // Check for incorrect memory config, overlapping addresses, etc.
            Memory.Validate();
        }
    }
}
