using System.Diagnostics;
using SadConsole.UI;
using SadConsole.UI.Controls;

namespace Highbyte.DotNet6502.App.SadConsole;

public enum FilePickerMode
{
    OpenFile,
    OpenFolder,
    SaveFile
};

public class FilePickerConsole : Window
{
    public const int CONSOLE_WIDTH = 40;
    public const int CONSOLE_HEIGHT = 20;

    private readonly FilePickerMode _filePickerMode;
    private readonly string _fileFilter;

    private DirectoryInfo _selectedDirectory;
    public DirectoryInfo SelectedDirectory => _selectedDirectory;
    private FileInfo? _selectedFile;
    public FileInfo? SelectedFile => _selectedFile;

    public FilePickerConsole(FilePickerMode filePickerMode, string defaultFolder, string defaultFile = "", string filter = "*.*") : base(CONSOLE_WIDTH, CONSOLE_HEIGHT)
    {
        _selectedDirectory = Directory.Exists(defaultFolder) ? new DirectoryInfo(defaultFolder) : new DirectoryInfo(Environment.CurrentDirectory);
        // TODO: Handle defaultFile
        _filePickerMode = filePickerMode;
        _fileFilter = filter;

        Title = filePickerMode switch
        {
            FilePickerMode.OpenFile => "Open File",
            FilePickerMode.OpenFolder => "Open Folder",
            FilePickerMode.SaveFile => "Save File",
            _ => "?"
        };

        Cursor.PrintAppearanceMatchesHost = false;
        Cursor.DisableWordBreak = true;
        Colors colors = Controls.GetThemeColors();
        Cursor.SetPrintAppearance(colors.Title, Surface.DefaultBackground);

        UseMouse = true;
        MouseMove += (s, e) =>
        {
        };
        UseKeyboard = true;

        DrawUIItems();
    }

    private void DrawUIItems()
    {

        FileDirectoryListbox fileListBox = new FileDirectoryListbox(30, Height - 7) { Name = "fileListBox" };
        fileListBox.FileFilter = _fileFilter;
        fileListBox.OnlyRootAndSubDirs = false;
        fileListBox.HideNonFilterFiles = false;
        fileListBox.Position = (1, 2);
        fileListBox.SelectedItemChanged += FileListBox_SelectedItemChanged;

        fileListBox.CurrentFolder = _selectedDirectory.FullName;

        if (_filePickerMode == FilePickerMode.OpenFolder)
        {
        }
        else if (_filePickerMode == FilePickerMode.OpenFile)
        {
        }
        else if (_filePickerMode == FilePickerMode.SaveFile)
        {
        }

        //((SadConsole.UI.Themes.ListBoxTheme)fileListBox.Theme).DrawBorder = true;
        Controls.Add(fileListBox);

        // TextBox for selected file or folder.
        var selectedItemTextBox = new TextBox(30)
        {
            Name = "selectedItemTextBox",
            Position = (1, Height - 4),
            Text = "",
            IsVisible = (_filePickerMode == FilePickerMode.OpenFile || _filePickerMode == FilePickerMode.SaveFile)
        };
        selectedItemTextBox.TextChanged += (s, e) =>
        {
            string textBoxValue = selectedItemTextBox.Text;

            if (_filePickerMode == FilePickerMode.OpenFile)
            {
                var fileFullPath = Path.Combine(_selectedDirectory.FullName, textBoxValue);
                if (File.Exists(fileFullPath))
                {
                    _selectedFile = new FileInfo(fileFullPath);
                }
                else
                {
                    _selectedFile = null;
                }
            }
            else if (_filePickerMode == FilePickerMode.SaveFile)
            {
                var fileFullPath = Path.Combine(_selectedDirectory.FullName, textBoxValue);
                _selectedFile = new FileInfo(fileFullPath);
            }
            else if (_filePickerMode == FilePickerMode.OpenFolder)
            {
                //string directoryFullPath;
                //if (_selectedDirectory.Parent != null) // Detect if current is a drive root (ex C:), then Parent will be null.
                //    directoryFullPath = Path.Combine(_selectedDirectory.Parent.FullName, textBoxValue);
                //else
                //    directoryFullPath = Path.Combine(_selectedDirectory.FullName, textBoxValue);
                //_selectedDirectory = new DirectoryInfo(directoryFullPath);
                //_selectedFile = null;
            }

            DebugPrintSelectedItems();

            IsDirty = true;

        };
        Controls.Add(selectedItemTextBox);

        // Add cancel and ok buttons
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

        SetControlStates(); // Trigger ok button state update.
    }

    private void FileListBox_SelectedItemChanged(object? sender, ListBox.SelectedItemEventArgs? e)
    {
        if (e.Item != null)
        {
            var selectedItemTextBox = Controls["selectedItemTextBox"] as TextBox;

            if (e.Item is FileDirectoryListbox.FauxDirectory fauxDirectory)
            {
                // Detect parent directory change (..)
                //Debug.WriteLine($"FauxDirectory: {fauxDirectory.Name}");

                var fileListBox = Controls["fileListBox"] as FileDirectoryListbox;
                if (_selectedDirectory.FullName != fileListBox.CurrentFolder)
                {
                    _selectedDirectory = new DirectoryInfo(fileListBox.CurrentFolder);
                    if (_filePickerMode == FilePickerMode.OpenFile || _filePickerMode == FilePickerMode.SaveFile)
                    {
                        if (_selectedFile != null && _selectedDirectory.FullName != _selectedFile.Directory!.FullName)
                            _selectedFile = new FileInfo(Path.Combine(_selectedDirectory.FullName, _selectedFile.Name));
                    }
                    else
                    {
                        // OpenFolder mode
                        //selectedItemTextBox.Text = _selectedDirectory.Name;
                        //selectedItemTextBox.IsDirty = true;
                    }
                }
            }

            if (e.Item is DirectoryInfo directoryInfo)
            {
                //Debug.WriteLine($"DirectoryInfo: {directoryInfo.FullName}");

                _selectedDirectory = directoryInfo;

                if (_filePickerMode == FilePickerMode.OpenFolder)
                {
                    //selectedItemTextBox.Text = _selectedDirectory.Name;
                    //selectedItemTextBox.IsDirty = true;
                }
                else if (_filePickerMode == FilePickerMode.OpenFile || _filePickerMode == FilePickerMode.SaveFile)
                {
                    if (_selectedFile != null && _selectedDirectory.FullName != _selectedFile.Directory.FullName)
                        _selectedFile = new FileInfo(Path.Combine(_selectedDirectory.FullName, _selectedFile.Name));
                }
            }
            if (e.Item is FileInfo fileInfo)
            {
                //Debug.WriteLine($"FileInfo: {fileInfo.FullName}");

                if (_filePickerMode == FilePickerMode.OpenFolder)
                    return;

                _selectedFile = fileInfo;
                _selectedDirectory = fileInfo.Directory!;

                selectedItemTextBox.Text = _selectedFile.Name;
                selectedItemTextBox.IsDirty = true;
            }

            DebugPrintSelectedItems();
            IsDirty = true;
        }
    }

    private void DebugPrintSelectedItems()
    {
        Debug.WriteLine($"SelectedDirectory: {_selectedDirectory.FullName}");
        var selectedFile = _selectedFile != null ? _selectedFile.FullName : "";
        Debug.WriteLine($"SelectedFile     : {selectedFile}");
        Debug.WriteLine("----------------------------------------");
    }

    protected override void OnIsDirtyChanged()
    {
        if (IsDirty)
        {
            SetControlStates();
        }
    }

    private void SetControlStates()
    {
        var okButton = Controls["okButton"] as Button;
        if (_filePickerMode == FilePickerMode.OpenFile)
        {
            if (_selectedFile != null)
            {
                if (_selectedFile.Directory.FullName != _selectedDirectory.FullName)
                {
                    // A file from another directory has been selected.
                    okButton.IsEnabled = false;
                }
                else
                {
                    okButton.IsEnabled = _selectedFile != null && _selectedFile.Exists;
                }
            }
            else
            {
                okButton.IsEnabled = false;
            }
        }
        else if (_filePickerMode == FilePickerMode.SaveFile)
        {
            okButton.IsEnabled = _selectedDirectory.Exists && _selectedFile != null;
        }
        else if (_filePickerMode == FilePickerMode.OpenFolder)
        {
            okButton.IsEnabled = _selectedDirectory.Exists;
        }
    }
}
