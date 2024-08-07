using Highbyte.DotNet6502.Systems.Commodore64.Config;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.App.SadConsole.ConfigUI;
public class C64ConfigUIConsole : Window
{
    public const int CONSOLE_WIDTH = USABLE_WIDTH;
    public const int CONSOLE_HEIGHT = USABLE_HEIGHT;
    private const int USABLE_WIDTH = 60;
    private const int USABLE_HEIGHT = 25;

    private readonly SadConsoleHostApp _sadConsoleHostApp;

    private C64Config _c64Config => (C64Config)_sadConsoleHostApp.GetSystemConfig().Result;

    public C64ConfigUIConsole(SadConsoleHostApp sadConsoleHostApp) : base(CONSOLE_WIDTH, CONSOLE_HEIGHT)
    {
        _sadConsoleHostApp = sadConsoleHostApp;

        Controls.ThemeColors = SadConsoleUISettings.ThemeColors;
        Surface.DefaultBackground = Controls.ThemeColors.ControlHostBackground;
        Surface.DefaultForeground = Controls.ThemeColors.ControlHostForeground;
        Surface.Clear();

        Title = "C64 Config";
        var colors = Controls.GetThemeColors();
        Cursor.PrintAppearanceMatchesHost = false;
        Cursor.DisableWordBreak = true;
        Cursor.SetPrintAppearance(colors.Title, Surface.DefaultBackground);

        UseMouse = true;
        UseKeyboard = true;

        DrawUIItems();
    }

    private void DrawUIItems()
    {
        // ROM directory
        var romDirectoryLabel = CreateLabel("ROM directory", 1, 1);
        var romDirectoryTextBox = new TextBox(Width - 10)
        {
            Name = "romDirectoryTextBox",
            Position = (1, romDirectoryLabel.Position.Y + 1),
        };
        romDirectoryTextBox.TextChanged += (s, e) => { _c64Config.ROMDirectory = romDirectoryTextBox.Text; IsDirty = true; };
        Controls.Add(romDirectoryTextBox);

        var selectROMDirectoryButton = new Button("...")
        {
            Name = "selectROMDirectoryButton",
            Position = (romDirectoryTextBox.Bounds.MaxExtentX + 2, romDirectoryTextBox.Position.Y),
        };
        selectROMDirectoryButton.Click += (s, e) => ShowROMFolderPickerDialog();
        Controls.Add(selectROMDirectoryButton);

        // Kernal ROM file
        var kernalROMLabel = CreateLabel("Kernal ROM", 1, romDirectoryTextBox.Position.Y + 2);
        var kernalROMTextBox = new TextBox(Width - 10)
        {
            Name = "kernalROMTextBox",
            Position = (1, kernalROMLabel.Position.Y + 1)
        };
        kernalROMTextBox.TextChanged += (s, e) => { _c64Config.SetROM(C64Config.KERNAL_ROM_NAME, kernalROMTextBox!.Text); IsDirty = true; };
        Controls.Add(kernalROMTextBox);

        var selectKernalROMButton = new Button("...")
        {
            Name = "selectKernalROMButton",
            Position = (kernalROMTextBox.Bounds.MaxExtentX + 2, kernalROMTextBox.Position.Y),
        };
        selectKernalROMButton.Click += (s, e) => ShowROMFilePickerDialog(C64Config.KERNAL_ROM_NAME);
        Controls.Add(selectKernalROMButton);

        // Basic ROM file
        var basicROMLabel = CreateLabel("Basic ROM", 1, kernalROMTextBox.Bounds.MaxExtentY + 2);
        var basicROMTextBox = new TextBox(Width - 10)
        {
            Name = "basicROMTextBox",
            Position = (1, basicROMLabel.Position.Y + 1)
        };
        basicROMTextBox.TextChanged += (s, e) => { _c64Config.SetROM(C64Config.BASIC_ROM_NAME, basicROMTextBox!.Text); IsDirty = true; };
        Controls.Add(basicROMTextBox);

        var selectBasicROMButton = new Button("...")
        {
            Name = "selectBasicROMButton",
            Position = (basicROMTextBox.Bounds.MaxExtentX + 2, basicROMTextBox.Position.Y),
        };
        selectBasicROMButton.Click += (s, e) => ShowROMFilePickerDialog(C64Config.BASIC_ROM_NAME);
        Controls.Add(selectBasicROMButton);

        // Chargen ROM file
        var chargenROMLabel = CreateLabel("Chargen ROM", 1, basicROMTextBox.Bounds.MaxExtentY + 2);
        var chargenROMTextBox = new TextBox(Width - 10)
        {
            Name = "chargenROMTextBox",
            Position = (1, chargenROMLabel.Position.Y + 1),
        };
        chargenROMTextBox.TextChanged += (s, e) => { _c64Config.SetROM(C64Config.CHARGEN_ROM_NAME, chargenROMTextBox!.Text); IsDirty = true; };
        Controls.Add(chargenROMTextBox);

        var selectChargenROMButton = new Button("...")
        {
            Name = "selectChargenROMButton",
            Position = (chargenROMTextBox.Bounds.MaxExtentX + 2, chargenROMTextBox.Position.Y),
        };
        selectChargenROMButton.Click += (s, e) => ShowROMFilePickerDialog(C64Config.CHARGEN_ROM_NAME);
        Controls.Add(selectChargenROMButton);


        // URL for downloading C64 ROMs
        var romDownloadsLabel = CreateLabel("ROM download link", 1, chargenROMTextBox.Bounds.MaxExtentY + 2);
        var romDownloadLinkTextBox = new TextBox(Width - 10)
        {
            Name = "romDownloadLinkTextBox",
            Position = (1, romDownloadsLabel.Bounds.MaxExtentY + 1),
            IsEnabled = false,
            Text = "https://www.commodore.ca/manuals/funet/cbm/firmware/computers/c64/index-t.html",
        };
        Controls.Add(romDownloadLinkTextBox);

        var openROMDownloadURLButton = new Button("...")
        {
            Name = "openROMDownloadURLButton",
            Position = (romDownloadLinkTextBox.Bounds.MaxExtentX + 2, romDownloadLinkTextBox.Position.Y),
        };
        openROMDownloadURLButton.Click += (s, e) => OpenURL(romDownloadLinkTextBox.Text);
        Controls.Add(openROMDownloadURLButton);


        // Validaton errors
        var validationErrorsLabel = CreateLabel("Validation errors", 1, romDownloadLinkTextBox.Bounds.MaxExtentY + 2, "validationErrorsLabel");
        var validationErrorsListBox = new ListBox(Width - 3, 3)
        {
            Name = "validationErrorsListBox",
            Position = (1, validationErrorsLabel.Bounds.MaxExtentY + 1),
            IsScrollBarVisible = true,
            IsEnabled = true,
        };
        Controls.Add(validationErrorsListBox);

        // Note: Currently Cancel button doesn't do anything, because the changes in this window are saved directly to the config object used by the emulator.
        //Button cancelButton = new Button(10, 1)
        //{
        //    Text = "Cancel",
        //    Position = (1, Height - 2)
        //};
        //cancelButton.Click += (s, e) => { DialogResult = false; Hide(); };
        //Controls.Add(cancelButton);

        var okButton = new Button(6, 1)
        {
            Text = "OK",
            Position = (Width - 1 - 7, Height - 2)
        };
        okButton.Click += (s, e) => { DialogResult = true; Hide(); };
        Controls.Add(okButton);


        // Helper function to create a label and add it to the console
        Label CreateLabel(string text, int col, int row, string? name = null)
        {
            var labelTemp = new Label(text) { Position = new Point(col, row), Name = name };
            Controls.Add(labelTemp);
            return labelTemp;
        }
        Label CreateLabelValue(string text, int col, int row, string? name = null)
        {
            var labelTemp = new Label(text) { Position = new Point(col, row), TextColor = Controls.GetThemeColors().Title, Name = name };
            Controls.Add(labelTemp);
            return labelTemp;
        }

        // Force OnIsDirtyChanged event which will set control states (see SetControlStates)
        OnIsDirtyChanged();
    }

    private void OpenURL(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new Exception($"Invalid URL: {url}");
        // Launch the URL in the default browser
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private void ShowROMFilePickerDialog(string romName)
    {
        var currentFolder = PathHelper.ExpandOSEnvironmentVariables(_c64Config.ROMDirectory);
        var window = new FilePickerConsole(FilePickerMode.OpenFile, currentFolder, _c64Config.GetROM(romName).GetROMFilePath(currentFolder));
        window.Center();
        window.Closed += (s2, e2) =>
        {
            if (window.DialogResult)
            {
                _c64Config.SetROM(romName, Path.GetFileName(window.SelectedFile.FullName));
                IsDirty = true;
            }
        };
        window.Show(true);
    }

    private void ShowROMFolderPickerDialog()
    {
        var currentFolder = PathHelper.ExpandOSEnvironmentVariables(_c64Config.ROMDirectory);
        var window = new FilePickerConsole(FilePickerMode.OpenFolder, currentFolder);
        window.Center();
        window.Closed += (s2, e2) =>
        {
            if (window.DialogResult)
            {
                _c64Config.ROMDirectory = window.SelectedDirectory.FullName;
                IsDirty = true;
            }
        };
        window.Show(true);
    }

    protected override void OnIsDirtyChanged()
    {
        if (IsDirty)
            SetControlStates();
    }

    private void SetControlStates()
    {
        var romDirectoryTextBox = Controls["romDirectoryTextBox"] as TextBox;
        romDirectoryTextBox!.Text = _c64Config.ROMDirectory;
        romDirectoryTextBox!.IsDirty = true;

        var kernalROMTextBox = Controls["kernalROMTextBox"] as TextBox;
        kernalROMTextBox!.Text = _c64Config.ROMs.SingleOrDefault(x => x.Name == C64Config.KERNAL_ROM_NAME).File;
        kernalROMTextBox!.IsDirty = true;

        var basicROMTextBox = Controls["basicROMTextBox"] as TextBox;
        basicROMTextBox!.Text = _c64Config.ROMs.SingleOrDefault(x => x.Name == C64Config.BASIC_ROM_NAME).File;
        basicROMTextBox!.IsDirty = true;

        var chargenROMTextBox = Controls["chargenROMTextBox"] as TextBox;
        chargenROMTextBox!.Text = _c64Config.ROMs.SingleOrDefault(x => x.Name == C64Config.CHARGEN_ROM_NAME).File;
        chargenROMTextBox!.IsDirty = true;

        (var isOk, var validationErrors) = _sadConsoleHostApp.IsValidConfigWithDetails().Result;
        var validationErrorsLabel = Controls["validationErrorsLabel"] as Label;
        validationErrorsLabel!.IsVisible = !isOk;
        var validationErrorsListBox = Controls["validationErrorsListBox"] as ListBox;
        validationErrorsListBox.IsVisible = !isOk;
        validationErrorsListBox!.Items.Clear();
        foreach (var error in validationErrors)
        {
            var coloredString = new ColoredString(error, foreground: Color.Red, background: Color.Black);
            validationErrorsListBox!.Items.Add(coloredString);
        }
    }
}
