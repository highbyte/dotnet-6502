using System;
using System.Diagnostics;
using Highbyte.DotNet6502.Systems.Generic;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.App.Monitor
{
    class Program
    {
        static ConsoleMonitor Monitor;
        static void Main(string[] args)
        {

            var mem = new Memory();

            var computerBuilder = new GenericComputerBuilder();
            computerBuilder
                .WithCPU()
                //.WithStartAddress()
                .WithMemory(mem)
                .WithInstructionExecutedEventHandler(
                    (s, e) => Debug.WriteLine(OutputGen.GetLastInstructionDisassembly(e.CPU, e.Mem)));
                // .WithExecOptions(options =>
                // {
                // });
            var computer = computerBuilder.Build();

            Monitor = new ConsoleMonitor(computer.CPU, computer.Mem);

            Monitor.ShowDescription();
            Monitor.WriteOutput("");
            Monitor.ShowHelp();

            bool cont = true;
            while (cont)
            {
                var input = PromptInput();
                Monitor.SendCommand(input);
                if (Monitor.Quit)
                    cont = false;
            }
        }

        private static string PromptInput()
        {
            return Prompt.GetString(">",
                promptColor: ConsoleColor.Gray,
                promptBgColor: ConsoleColor.DarkBlue);

            // Console.Write(">");
            // return Console.ReadLine();
        }
    }
}
