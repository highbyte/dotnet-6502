namespace Highbyte.DotNet6502.Systems
{
    public interface ISystem
    {
        CPU CPU { get; set; }
        Memory Mem { get; set; }

        public bool ExecuteOneInstruction();
        public bool ExecuteOneFrame();
    }
}