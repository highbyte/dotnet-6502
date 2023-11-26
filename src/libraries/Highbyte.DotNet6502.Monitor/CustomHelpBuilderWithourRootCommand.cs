using System.CommandLine;
using System.CommandLine.Help;
using System.Text;

namespace Highbyte.DotNet6502.Monitor;

/// <summary>
/// HelpBuilder that removes the root command name from the help text.
/// </summary>
internal class CustomHelpBuilderWithourRootCommand : HelpBuilder
{
    private readonly string _rootCommandName;

    internal CustomHelpBuilderWithourRootCommand(
        LocalizationResources localizationResources,
        string rootCommandName,
        int maxWidth = int.MaxValue)
        : base(localizationResources, maxWidth)
    {
        _rootCommandName = rootCommandName;
    }

    public override void Write(HelpContext context)
    {
        var customTextWriter = new CustomTextWriterWithoutRootCommand(context.Output, _rootCommandName);
        var newHelpContext = new HelpContext(context.HelpBuilder, context.Command, customTextWriter, context.ParseResult);

        base.Write(newHelpContext);
    }

    private class CustomTextWriterWithoutRootCommand : TextWriter
    {
        private readonly TextWriter _originalTextWriter;
        private readonly string _rootCommandName;

        public CustomTextWriterWithoutRootCommand(TextWriter originalTextWriter, string rootCommandName)
        {
            _originalTextWriter = originalTextWriter;
            _rootCommandName = rootCommandName;
        }

        public override void Write(char value)
        {
            _originalTextWriter.Write(value);
        }

        public override void Write(string? value)
        {
            if (value != null && value.StartsWith(_rootCommandName))
            {
                value = value.Replace(_rootCommandName, "").TrimStart();
            }
            _originalTextWriter.Write(value);
        }

        public override Encoding Encoding => _originalTextWriter.Encoding;
    }
}
