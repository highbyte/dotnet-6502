using System;
using Highbyte.DotNet6502.Monitor.Commands;
using McMaster.Extensions.CommandLineUtils;

namespace Highbyte.DotNet6502.Monitor
{
    class Program
    {
        static Mon Mon;
        static void Main(string[] args)
        {
            Mon = new Mon();
            var commandLineApp = FluentCommands.Configure(Mon);

            Console.WriteLine(commandLineApp.Description);
            Console.WriteLine("");

            commandLineApp.ShowHelp();
            
            bool cont = true;
            while(cont)
            {
                var input = PromptInput();
                if(input==null)
                    input = "";
                if(!string.IsNullOrEmpty(input))
                {
                    if(string.Equals(input, "?", StringComparison.InvariantCultureIgnoreCase) 
                        || string.Equals(input, "-?", StringComparison.InvariantCultureIgnoreCase)
                        || string.Equals(input, "help", StringComparison.InvariantCultureIgnoreCase)
                        || string.Equals(input, "--help", StringComparison.InvariantCultureIgnoreCase)
                    )
                    {
                        commandLineApp.ShowHelp();
                    }
                    else
                    {
                        // Workaround for CommandLineUtils after showing help once, it will always show it for every command, even if syntax is correct.
                        // Create new instance for every time we parse input
                        commandLineApp = FluentCommands.Configure(Mon);
                        int result = commandLineApp.Execute(input.Split(' '));
                        if(result==2)
                            cont = false;
                    }
                }
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
