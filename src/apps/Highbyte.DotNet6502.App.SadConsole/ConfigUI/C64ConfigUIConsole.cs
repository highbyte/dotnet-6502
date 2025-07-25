using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.App.SadConsole.SystemSetup;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Utils.BasicAssistant;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Configuration;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.App.SadConsole.ConfigUI;
public class C64ConfigUIConsole : Window
{
    public const int CONSOLE_WIDTH = USABLE_WIDTH;
    public const int CONSOLE_HEIGHT = USABLE_HEIGHT;
    private const int USABLE_WIDTH = 60;
    private const int USABLE_HEIGHT = 28;

    public C64SystemConfig C64SystemConfig => C64HostConfig.SystemConfig;
    public readonly C64HostConfig C64HostConfig;
    private readonly IConfiguration _configuration;

    public C64ConfigUIConsole(SadConsoleHostApp sadConsoleHostApp, IConfiguration configuration) : base(CONSOLE_WIDTH, CONSOLE_HEIGHT)
    {

        C64HostConfig = (C64HostConfig)sadConsoleHostApp.CurrentHostSystemConfig.Clone();

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
        _configuration = configuration;
    }

    private void DrawUIItems()
    {
        // Automatic download of C64 ROMs
        var autoDownloadROMButton = new Button("Auto download ROM files")
        {
            Name = "autoDownloadROMButton",
            Position = (1, 2),
        };
        autoDownloadROMButton.Click += async (s, e) =>
        {
            var autoDownloadROMInfoLabel = Controls["autoDownloadROMInfoLabel"] as Label;
            try
            {
                await AutoDownloadROMs();
                autoDownloadROMInfoLabel.DisplayText = "ROMs downloaded OK";
                autoDownloadROMInfoLabel.TextColor = Color.Green;
            }
            catch (Exception ex)
            {
                autoDownloadROMInfoLabel.DisplayText = ex.Message;
                autoDownloadROMInfoLabel.TextColor = Color.Red;
            }
            finally
            {
                IsDirty = true; // Mark as dirty to update the UI
            }
        };
        Controls.Add(autoDownloadROMButton);

        // Manual download link
        var openROMDownloadURLButton = new Button("Manual ROM download link")
        {
            Name = "openROMDownloadURLButton",
            Position = (31, autoDownloadROMButton.Position.Y),
        };
        openROMDownloadURLButton.Click += (s, e) => OpenURL(new Uri(C64SystemConfig.ROMDownloadUrls[C64SystemConfig.KERNAL_ROM_NAME]).GetLeftPart(UriPartial.Authority));
        Controls.Add(openROMDownloadURLButton);

        // Auto ROM download status label
        var autoDownloadROMInfoLabel = new Label(Width - 10)
        {
            Name = "autoDownloadROMInfoLabel",
            Position = (1, autoDownloadROMButton.Bounds.MaxExtentY + 1),
            IsEnabled = false,
            DisplayText = "",
            TextColor = Controls.GetThemeColors().Appearance_ControlDisabled.Foreground
        };
        Controls.Add(autoDownloadROMInfoLabel);


        // ROM directory
        var romDirectoryLabel = CreateLabel("ROM directory", 1, autoDownloadROMInfoLabel.Position.Y + 2);
        var romDirectoryTextBox = new TextBox(Width - 10)
        {
            Name = "romDirectoryTextBox",
            Position = (1, romDirectoryLabel.Position.Y + 1),
        };
        romDirectoryTextBox.TextChanged += (s, e) => { C64SystemConfig.ROMDirectory = romDirectoryTextBox.Text; IsDirty = true; };
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
        kernalROMTextBox.TextChanged += (s, e) => { C64SystemConfig.SetROM(C64SystemConfig.KERNAL_ROM_NAME, kernalROMTextBox!.Text); IsDirty = true; };
        Controls.Add(kernalROMTextBox);

        var selectKernalROMButton = new Button("...")
        {
            Name = "selectKernalROMButton",
            Position = (kernalROMTextBox.Bounds.MaxExtentX + 2, kernalROMTextBox.Position.Y),
        };
        selectKernalROMButton.Click += (s, e) => ShowROMFilePickerDialog(C64SystemConfig.KERNAL_ROM_NAME);
        Controls.Add(selectKernalROMButton);

        // Basic ROM file
        var basicROMLabel = CreateLabel("Basic ROM", 1, kernalROMTextBox.Bounds.MaxExtentY + 2);
        var basicROMTextBox = new TextBox(Width - 10)
        {
            Name = "basicROMTextBox",
            Position = (1, basicROMLabel.Position.Y + 1)
        };
        basicROMTextBox.TextChanged += (s, e) => { C64SystemConfig.SetROM(C64SystemConfig.BASIC_ROM_NAME, basicROMTextBox!.Text); IsDirty = true; };
        Controls.Add(basicROMTextBox);

        var selectBasicROMButton = new Button("...")
        {
            Name = "selectBasicROMButton",
            Position = (basicROMTextBox.Bounds.MaxExtentX + 2, basicROMTextBox.Position.Y),
        };
        selectBasicROMButton.Click += (s, e) => ShowROMFilePickerDialog(C64SystemConfig.BASIC_ROM_NAME);
        Controls.Add(selectBasicROMButton);

        // Chargen ROM file
        var chargenROMLabel = CreateLabel("Chargen ROM", 1, basicROMTextBox.Bounds.MaxExtentY + 2);
        var chargenROMTextBox = new TextBox(Width - 10)
        {
            Name = "chargenROMTextBox",
            Position = (1, chargenROMLabel.Position.Y + 1),
        };
        chargenROMTextBox.TextChanged += (s, e) => { C64SystemConfig.SetROM(C64SystemConfig.CHARGEN_ROM_NAME, chargenROMTextBox!.Text); IsDirty = true; };
        Controls.Add(chargenROMTextBox);

        var selectChargenROMButton = new Button("...")
        {
            Name = "selectChargenROMButton",
            Position = (chargenROMTextBox.Bounds.MaxExtentX + 2, chargenROMTextBox.Position.Y),
        };
        selectChargenROMButton.Click += (s, e) => ShowROMFilePickerDialog(C64SystemConfig.CHARGEN_ROM_NAME);
        Controls.Add(selectChargenROMButton);


        // AI coding assistant selection
        var codingAssistantLabel = CreateLabel("Basic AI assistant: ", 1, selectChargenROMButton.Bounds.MaxExtentY + 2);
        var codingAssistantValue = CreateLabel($"{C64HostConfig.CodeSuggestionBackendType}", codingAssistantLabel.Bounds.MaxExtentX + 1, codingAssistantLabel.Position.Y);
        codingAssistantValue.TextColor = Controls.GetThemeColors().White;

        var codingAssistantInfoLabel = new Label(Width - 10)
        {
            Name = "codingAssistantInfoLabel",
            Position = (1, codingAssistantValue.Bounds.MaxExtentY + 1),
            IsEnabled = false,
            DisplayText = "Set AI assistant in appsetting.json.",
            TextColor = Controls.GetThemeColors().Appearance_ControlDisabled.Foreground
        };
        Controls.Add(codingAssistantInfoLabel);

        var codingAssistantTestButton = new Button("Test")
        {
            Name = "codingAssistantTestButton",
            Position = (codingAssistantValue.Bounds.MaxExtentX + 2, codingAssistantValue.Position.Y),
        };
        codingAssistantTestButton.Click += async (s, e) =>
        {
            try
            {
                var codeSuggestionBackend = CodeSuggestionConfigurator.CreateCodeSuggestion(C64HostConfig.CodeSuggestionBackendType, _configuration, C64BasicCodingAssistant.CODE_COMPLETION_LANGUAGE_DESCRIPTION, C64BasicCodingAssistant.CODE_COMPLETION_ADDITIONAL_SYSTEM_INSTRUCTION);
                codingAssistantInfoLabel.DisplayText = "Testing...";
                codingAssistantInfoLabel.TextColor = Color.White;

                await codeSuggestionBackend.CheckAvailability();
                if (codeSuggestionBackend.IsAvailable)
                {
                    codingAssistantInfoLabel.DisplayText = "OK";
                    codingAssistantInfoLabel.TextColor = Color.Green;
                }
                else
                {
                    codingAssistantInfoLabel.DisplayText = codeSuggestionBackend.LastError ?? "Error";
                    codingAssistantInfoLabel.TextColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                codingAssistantInfoLabel.DisplayText = ex.Message;
                codingAssistantInfoLabel.TextColor = Color.Red;
            }
        };
        Controls.Add(codingAssistantTestButton);

        var openBasicAIHelpURLButton = new Button("Help")
        {
            Name = "openBasicAIHelpURLButton",
            Position = (codingAssistantTestButton.Bounds.MaxExtentX, codingAssistantInfoLabel.Position.Y),
        };
        openBasicAIHelpURLButton.Click += (s, e) => OpenURL("https://github.com/highbyte/dotnet-6502/blob/master/doc/SYSTEMS_C64_AI_CODE_COMPLETION.md");
        Controls.Add(openBasicAIHelpURLButton);


        //ComboBox codingAssistantComboBox = new ComboBox(codingAssistantLabel.Bounds.MaxExtentX + 1, codingAssistantLabel.Position.Y, 6, Enum.GetNames<CodeSuggestionBackendTypeEnum>().ToArray())
        //{
        //    Position = (codingAssistantLabel.Bounds.MaxExtentX + 2, codingAssistantLabel.Position.Y),
        //    Name = "codingAssistantComboBox",
        //    SelectedItem = C64HostConfig.CodeSuggestionBackendType.ToString(),
        //};
        //codingAssistantComboBox.SelectedItemChanged += (s, e) =>
        //{
        //    C64HostConfig.CodeSuggestionBackendType = (CodeSuggestionBackendTypeEnum)codingAssistantComboBox.SelectedItem;
        //    IsDirty = true;
        //};
        //Controls.Add(codingAssistantComboBox);


        // Validaton errors
        var validationErrorsLabel = CreateLabel("Validation errors", 1, codingAssistantInfoLabel.Bounds.MaxExtentY + 2, "validationErrorsLabel");
        var validationErrorsListBox = new ListBox(Width - 3, 3)
        {
            Name = "validationErrorsListBox",
            Position = (1, validationErrorsLabel.Bounds.MaxExtentY + 1),
            IsScrollBarVisible = true,
            IsEnabled = true,
        };
        Controls.Add(validationErrorsListBox);

        var okButton = new Button(6, 1)
        {
            Name = "okButton",
            Text = "OK",
            Position = (1, Height - 2)
        };
        okButton.Click += (s, e) => { DialogResult = true; Hide(); };
        Controls.Add(okButton);

        var cancelButton = new Button(10, 1)
        {
            Text = "Cancel",
            Position = (okButton.Bounds.MaxExtentX + 2, Height - 2)
        };
        cancelButton.Click += (s, e) => { DialogResult = false; Hide(); };
        Controls.Add(cancelButton);


        // Helper function to create a label and add it to the console
        Label CreateLabel(string text, int col, int row, string? name = null)
        {
            var labelTemp = new Label(text) { Position = new Point(col, row), Name = name };
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
        var currentFolder = PathHelper.ExpandOSEnvironmentVariables(C64SystemConfig.ROMDirectory);
        var window = new FilePickerConsole(FilePickerMode.OpenFile, currentFolder, C64SystemConfig.GetROM(romName).GetROMFilePath(currentFolder));
        window.Center();
        window.Closed += (s2, e2) =>
        {
            if (window.DialogResult)
            {
                C64SystemConfig.SetROM(romName, Path.GetFileName(window.SelectedFile!.FullName));
                IsDirty = true;
            }
        };
        window.Show(true);
    }

    private void ShowROMFolderPickerDialog()
    {
        var currentFolder = PathHelper.ExpandOSEnvironmentVariables(C64SystemConfig.ROMDirectory);
        var window = new FilePickerConsole(FilePickerMode.OpenFolder, currentFolder);
        window.Center();
        window.Closed += (s2, e2) =>
        {
            if (window.DialogResult)
            {
                C64SystemConfig.ROMDirectory = window.SelectedDirectory.FullName;
                IsDirty = true;
            }
        };
        window.Show(true);
    }

    private async Task AutoDownloadROMs()
    {
        var romFolder = PathHelper.ExpandOSEnvironmentVariables(C64SystemConfig.ROMDirectory);
        if (!Directory.Exists(romFolder))
        {
            Directory.CreateDirectory(romFolder);
        }

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        //httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");

        foreach (var romDownload in C64SystemConfig.ROMDownloadUrls)
        {
            var romName = romDownload.Key;
            var romUrl = romDownload.Value;
            var filename = Path.GetFileName(new Uri(romUrl).LocalPath);
            var dest = Path.Combine(romFolder, filename);
            try
            {
                using var response = await httpClient.GetAsync(romUrl);
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Failed to get '{romUrl}' ({(int)response.StatusCode})");
                await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
                System.Console.WriteLine($"Downloaded {filename} to {dest}");

                // Update the C64SystemConfig with the downloaded ROM file
                C64SystemConfig.SetROM(romName, filename);
            }
            catch (Exception ex)
            {
                if (File.Exists(dest))
                    File.Delete(dest);
                throw new Exception($"Error downloading {romUrl}: {ex.Message}", ex);
            }
        }
    }

    protected override void OnIsDirtyChanged()
    {
        if (IsDirty)
            SetControlStates();
    }

    private void SetControlStates()
    {
        var romDirectoryTextBox = Controls["romDirectoryTextBox"] as TextBox;
        romDirectoryTextBox!.Text = C64SystemConfig.ROMDirectory;
        romDirectoryTextBox!.IsDirty = true;

        var kernalROMTextBox = Controls["kernalROMTextBox"] as TextBox;
        kernalROMTextBox!.Text = C64SystemConfig.ROMs.Single(x => x.Name == C64SystemConfig.KERNAL_ROM_NAME).File!;
        kernalROMTextBox!.IsDirty = true;

        var basicROMTextBox = Controls["basicROMTextBox"] as TextBox;
        basicROMTextBox!.Text = C64SystemConfig.ROMs.Single(x => x.Name == C64SystemConfig.BASIC_ROM_NAME).File!;
        basicROMTextBox!.IsDirty = true;

        var chargenROMTextBox = Controls["chargenROMTextBox"] as TextBox;
        chargenROMTextBox!.Text = C64SystemConfig.ROMs.Single(x => x.Name == C64SystemConfig.CHARGEN_ROM_NAME).File!;
        chargenROMTextBox!.IsDirty = true;

        var codingAssistantTestButton = Controls["codingAssistantTestButton"] as Button;
        codingAssistantTestButton.IsEnabled = C64HostConfig.CodeSuggestionBackendType != CodeSuggestionBackendTypeEnum.None;

        var isOk = C64SystemConfig.IsValid(out List<string> validationErrors);
        var okButton = Controls["okButton"] as Button;
        okButton!.IsEnabled = isOk;

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
