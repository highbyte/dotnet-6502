using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.App.SadConsole.Core;

/// <summary>
/// Dialog window for displaying error messages with Continue/Quit actions.
/// </summary>
public class ErrorDialog : Window
{
    public const int DIALOG_WIDTH = 60;
    public const int DIALOG_HEIGHT = 15;

    public enum ErrorDialogResult
    {
        Continue,
        Exit
    }

    public ErrorDialogResult UserChoice { get; private set; } = ErrorDialogResult.Exit;

    /// <summary>
    /// Creates the error dialog.
    /// </summary>
    /// <param name="errorMessage">Message shown to the user.</param>
    /// <param name="exception">Optional exception whose type/message are shown as details.</param>
    /// <param name="fatalStartupError">When true the app cannot continue (e.g. no emulator systems
    /// available): the "Continue" button is removed and the only action is to quit.</param>
    public ErrorDialog(string errorMessage, Exception? exception = null, bool fatalStartupError = false)
        : base(DIALOG_WIDTH, DIALOG_HEIGHT)
    {
        Title = fatalStartupError ? "Startup Error" : "Application Error";

        Cursor.PrintAppearanceMatchesHost = false;
        Cursor.DisableWordBreak = true;
        Colors colors = Controls.GetThemeColors();
        Cursor.SetPrintAppearance(colors.Title, Surface.DefaultBackground);

        UseMouse = true;
        UseKeyboard = true;
        CanDrag = false;

        DrawUIItems(errorMessage, exception, fatalStartupError);

        // Center the dialog on screen
        Center();
    }

    private void DrawUIItems(string errorMessage, Exception? exception, bool fatalStartupError)
    {
        // Error icon/title
        var errorLabel = new Label("ERROR!")
        {
            Position = (2, 2),
            TextColor = Color.Red
        };
        Controls.Add(errorLabel);

        // Main error message - split into multiple lines if needed
        var lines = WrapText(errorMessage, Width - 4);
        int currentY = 4;
        for (int i = 0; i < Math.Min(lines.Length, 6); i++)
        {
            var messageLabel = new Label(lines[i])
            {
                Position = (2, currentY + i)
            };
            Controls.Add(messageLabel);
        }
        currentY += Math.Min(lines.Length, 6);

        // Exception details (if provided)
        if (exception != null)
        {
            var exceptionLabel = new Label("Exception Details:")
            {
                Position = (2, currentY),
                TextColor = Color.Yellow
            };
            Controls.Add(exceptionLabel);
            currentY++;

            var exceptionText = $"{exception.GetType().Name}: {exception.Message}";
            var exceptionLines = WrapText(exceptionText, Width - 4);
            for (int i = 0; i < Math.Min(exceptionLines.Length, 2); i++)
            {
                var exceptionDetailLabel = new Label(exceptionLines[i])
                {
                    Position = (2, currentY + i),
                    TextColor = Color.Gray
                };
                Controls.Add(exceptionDetailLabel);
            }
        }

        // Continue button — omitted for a fatal startup error, where quitting is the only option.
        Button? continueButton = null;
        if (!fatalStartupError)
        {
            continueButton = new Button(12, 1)
            {
                Name = "continueButton",
                Text = "Continue",
                Position = (Width / 2 - 14, Height - 2)
            };
            continueButton.Click += (s, e) =>
            {
                UserChoice = ErrorDialogResult.Continue;
                DialogResult = true;
                Hide();
            };
            Controls.Add(continueButton);
        }

        // Quit button. Centered when it is the only button (fatal startup error).
        var exitButton = new Button(8, 1)
        {
            Name = "exitButton",
            Text = "Quit",
            Position = fatalStartupError ? (Width / 2 - 4, Height - 2) : (Width / 2 + 2, Height - 2)
        };
        exitButton.Click += (s, e) =>
        {
            UserChoice = ErrorDialogResult.Exit;
            DialogResult = true;
            Hide();
        };
        Controls.Add(exitButton);

        // Set initial focus.
        if (continueButton != null)
            continueButton.IsFocused = true;
        else
            exitButton.IsFocused = true;
    }

    private string[] WrapText(string text, int maxWidth)
    {
        if (string.IsNullOrEmpty(text))
            return new string[0];

        var lines = new List<string>();

        // Honour explicit line breaks first (a SadConsole Label renders a raw '\n' as a glyph),
        // then word-wrap each resulting paragraph.
        foreach (var paragraph in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (paragraph.Length == 0)
            {
                // A blank line. Use a single space, not an empty string: a SadConsole Label
                // built from an empty string creates a zero-width surface and throws.
                lines.Add(" ");
                continue;
            }

            var currentLine = "";
            foreach (var word in paragraph.Split(' '))
            {
                if (currentLine.Length + word.Length + 1 <= maxWidth)
                {
                    if (currentLine.Length > 0)
                        currentLine += " ";
                    currentLine += word;
                }
                else
                {
                    if (currentLine.Length > 0)
                    {
                        lines.Add(currentLine);
                        currentLine = word;
                    }
                    else
                    {
                        // Word is too long, truncate it
                        lines.Add(word.Substring(0, Math.Min(word.Length, maxWidth)));
                        currentLine = "";
                    }
                }
            }

            if (currentLine.Length > 0)
                lines.Add(currentLine);
        }

        return lines.ToArray();
    }
}
