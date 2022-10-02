using Highbyte.DotNet6502.Systems.Generic;

namespace Highbyte.DotNet6502.Tests.Helpers
{
    public class TestContext
    {
        public GenericComputer Computer { get; private set;}
        public GenericComputer OriginalComputer { get; private set;} 
        private TestContext(){}
        public static TestContext NewTestContext(ushort startPos = 0x1000, int memorySize = 1024*64)
        {
            var builder = new GenericComputerBuilder()
            .WithCPU()
            .WithMemory(memorySize);

            var computer = builder.Build();
            var computerCopy = computer.Clone();

            return new TestContext 
            {
                Computer = computer,
                OriginalComputer = computerCopy
            };
        }
    }
}
