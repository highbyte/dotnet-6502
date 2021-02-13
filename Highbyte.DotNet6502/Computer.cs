namespace Highbyte.DotNet6502
{
    public class Computer
    {
        public Memory Mem { get; set;}
        public CPU CPU { get ; set; }
        public ExecOptions ExecOptions { get ; set; }
        public Computer()
        {
            Mem = new Memory();
            CPU = new CPU();
            ExecOptions = new ExecOptions();
        }

        public Computer(Memory mem, CPU cpu, ExecOptions execOptions)
        {
            Mem = mem;
            CPU = cpu;
            ExecOptions = execOptions;
        }

        public Computer Clone()
        {
            return new Computer()
            {
                CPU = this.CPU.Clone(),
                Mem = this.Mem.Clone(),
                ExecOptions = this.ExecOptions.Clone()
            };
        }

        public void Reset(ushort? cpuStartPos = null)
        {
            // TODO: Leave memory intact after reset?

            if(cpuStartPos==null)
                cpuStartPos = Mem.FetchWord(CPU.ResetVector);

            CPU.PC = cpuStartPos.Value;
        }

        public void Run()
        {
            CPU.Execute(
                this.Mem,
                this.ExecOptions
                );
        }
    }
}