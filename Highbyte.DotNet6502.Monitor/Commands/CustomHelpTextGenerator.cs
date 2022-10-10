using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.HelpText;

namespace Highbyte.DotNet6502.Monitor.Commands
{
    internal class CustomHelpTextGenerator : DefaultHelpTextGenerator
    {
        /// <summary>
        /// Override GenerateHeader to avoid name/description of the application to be shown each time help text is shown.
        /// </summary>
        /// <param name="application"></param>
        /// <param name="output"></param>
        protected override void GenerateHeader(CommandLineApplication application, TextWriter output)
        {
        }
    }
}