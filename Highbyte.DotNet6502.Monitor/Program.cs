using System;
using Highbyte.DotNet6502.Monitor.Commands;

namespace Highbyte.DotNet6502.Monitor
{
    class Program
    {
        static Mon Mon;
        static void Main(string[] args)
        {
            Mon = new Mon();

            Console.WriteLine("Highbyte.DotNet6502 machine code monitor");
            Console.WriteLine("");

            var commandLineApp = FluentCommands.Configure(Mon);
            bool cont = true;
            while(cont)
            {
                Console.WriteLine("");
                var input = PromptInput();
                int result = commandLineApp.Execute(input.Split(' '));
                if(result==2)
                    cont = false;
            }
        }

        private static string PromptInput()
        {
            Console.Write(">");
            return Console.ReadLine();
        }
    }
}
