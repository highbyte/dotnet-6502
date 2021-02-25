namespace Highbyte.DotNet6502.SadConsoleHost
{
    public class EmulatorMemoryBanksConfig
    {
        public bool EnableMemoryBanks { get; set; }
        public byte BanksPerSegment { get; set; }

        public EmulatorMemoryBanksConfig()
        {
            EnableMemoryBanks = false;
            BanksPerSegment = 1;
        }
    }
}