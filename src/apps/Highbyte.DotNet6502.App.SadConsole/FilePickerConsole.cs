using SadConsole.UI;
using SadConsole.UI.Controls;

namespace Highbyte.DotNet6502.App.SadConsole;
public class FilePickerConsole : Window
{
    public const int CONSOLE_WIDTH = 40;
    public const int CONSOLE_HEIGHT = 20;

    private readonly string _defaultFolder;
    private readonly string _defaultFile;
    private readonly bool _selectFolder;
    private string? _selectedFile;
    public string? SelectedFile => _selectedFile;

    private FilePickerConsole(string defaultFolder, string defaultFile = "", bool selectFolder = false) : base(CONSOLE_WIDTH, CONSOLE_HEIGHT)
    {
        _defaultFolder = defaultFolder;
        _defaultFile = defaultFile;
        _selectFolder = selectFolder;
    }

    public static FilePickerConsole Create(string defaultFolder, string defaultFile = "", bool selectFolder = false)
    {
        var console = new FilePickerConsole(defaultFolder, defaultFile, selectFolder);

        console.Title = selectFolder ? "Select folder" : "Select file";
        Colors colors = console.Controls.GetThemeColors();

        console.Cursor.PrintAppearanceMatchesHost = false;
        console.Cursor.DisableWordBreak = true;
        console.Cursor.SetPrintAppearance(colors.Title, console.Surface.DefaultBackground);


        console.UseMouse = true;
        console.MouseMove += (s, e) =>
        {
        };
        console.UseKeyboard = true;

        console.DrawUIItems();

        return console;
    }

    private void DrawUIItems()
    {
        string currentFolder;
        if (!Directory.Exists(_defaultFolder))
            currentFolder = Environment.CurrentDirectory;
        else
            currentFolder = _defaultFolder;

        FileDirectoryListbox fileListBox = new FileDirectoryListbox(30, Height - 5);
        fileListBox.FileFilter = "*.*";
        fileListBox.OnlyRootAndSubDirs = false;
        fileListBox.HideNonFilterFiles = false;
        fileListBox.Position = (1, 2);
        fileListBox.SelectedItemChanged += FileListBox_SelectedItemChanged;

        if (_selectFolder)
        {
            var parentFolder = Directory.GetParent(currentFolder);
            if (parentFolder != null)
            {
                fileListBox.CurrentFolder = parentFolder.FullName;
                //fileListBox.SelectedItem = Path.GetFileName(currentFolder);
                //fileListBox.ScrollToSelectedItem();
            }
            else
            {
                fileListBox.CurrentFolder = currentFolder;
            }
        }
        else
        {
            fileListBox.CurrentFolder = currentFolder;
            if (File.Exists(_defaultFile))
            {
                //fileListBox.SelectedItem = _defaultFile;
                //fileListBox.ScrollToSelectedItem();
            }
        }

        //((SadConsole.UI.Themes.ListBoxTheme)fileListBox.Theme).DrawBorder = true;
        Controls.Add(fileListBox);

        Button cancelButton = new Button(10, 1)
        {
            Name = "cancelButton",
            Text = "Cancel",
            Position = (1, Height - 2)
        };
        cancelButton.Click += (s, e) => { DialogResult = false; Hide(); };
        Controls.Add(cancelButton);

        Button okButton = new Button(6, 1)
        {
            Name = "okButton",
            Text = "OK",
            Position = (Width - 1 - 7, Height - 2),
            IsEnabled = false
        };
        okButton.Click += (s, e) => { DialogResult = true; Hide(); };
        Controls.Add(okButton);
    }

    private void FileListBox_SelectedItemChanged(object? sender, ListBox.SelectedItemEventArgs? e)
    {
        if (e.Item != null)
        {
            var okButton = Controls["okButton"] as Button;
            string selectedItem = e.Item.ToString();
            if (_selectFolder)
            {
                if (Directory.Exists(selectedItem))
                {
                    okButton.IsEnabled = true;
                    _selectedFile = selectedItem;
                }
                else
                {
                    okButton.IsEnabled = false;
                }
            }
            else
            {
                if (File.Exists(selectedItem))
                {
                    okButton.IsEnabled = true;
                    _selectedFile = selectedItem;
                }
                else
                {
                    okButton.IsEnabled = false;
                }
            }
        }

    }
}
