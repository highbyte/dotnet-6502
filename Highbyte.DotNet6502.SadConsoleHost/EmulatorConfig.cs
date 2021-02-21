using System;

namespace Highbyte.DotNet6502.SadConsoleHost
{
    public class EmulatorConfig
    {
        public string ProgramBinaryFile  { get; set; }
        public bool StopAtBRK { get; set; }
        public int RunEmulatorEveryFrame { get; set; }
        public EmulatorMemoryConfig Memory { get; set; }

        public EmulatorConfig()
        {
            Memory = new();
            StopAtBRK = true;
            RunEmulatorEveryFrame = 1;  // Runs every frame
        }

        public void Validate()
        {
            if(string.IsNullOrEmpty(ProgramBinaryFile))
                throw new Exception($"Setting {nameof(ProgramBinaryFile)} must be specified.");

             // Check for incorrect memory config, overlapping addresses, etc.
            Memory.Validate();
             
        }
    }
}
