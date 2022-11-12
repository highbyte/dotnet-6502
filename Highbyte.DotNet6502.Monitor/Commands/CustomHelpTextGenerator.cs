using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.HelpText;

namespace Highbyte.DotNet6502.Monitor.Commands
{
    internal class CustomHelpTextGenerator : DefaultHelpTextGenerator
    {
        public CustomHelpTextGenerator(int? maxLineLength = null)
        {
            // To make McMaster.Extensions.CommandLineUtils work in WASM, we have to set value for MaxLineLength in DefaultHelpTextGenerator
            // because otherwise it will try to call Console.BufferWidth, which will throw exception under WASM.
            base.MaxLineLength = maxLineLength;
        }

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
