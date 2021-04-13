namespace Highbyte.DotNet6502
{
    public class Computer
    {
        public Memory Mem { get; set;}
        public CPU CPU { get ; set; }
        public ExecOptions DefaultExecOptions { get ; set; }
        public Computer()
        {
            Mem = new Memory();
            CPU = new CPU();
            DefaultExecOptions = new ExecOptions();
        }

        public Computer(Memory mem, CPU cpu, ExecOptions defaultExecOptions)
        {
            Mem = mem;
            CPU = cpu;
            DefaultExecOptions = defaultExecOptions;
        }

        public Computer Clone()
        {
            return new Computer()
            {
                CPU = this.CPU.Clone(),
                Mem = this.Mem.Clone(),
                DefaultExecOptions = this.DefaultExecOptions.Clone()
            };
        }

        public void Reset(ushort? cpuStartPos = null)
        {
            // TODO: Leave memory intact after reset?

            if(cpuStartPos==null)
                cpuStartPos = Mem.FetchWord(CPU.ResetVector);

            CPU.PC = cpuStartPos.Value;
        }

        public ExecState Run()
        {
            return CPU.Execute(
                this.Mem,
                this.DefaultExecOptions
                );
        }

        public ExecState Run(ExecOptions execOptions)
        {
            return CPU.Execute(
                this.Mem,
                execOptions
                );
                
        }        
    }
}