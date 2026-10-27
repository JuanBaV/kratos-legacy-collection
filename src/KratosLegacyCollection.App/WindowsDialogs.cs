using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace KratosLegacyCollection.App;

public interface IUserDialogs
{
    string? File(string title, string filter);
    string? Folder(string title);
    string? Save(string title, string filter, string name);
    bool Confirm(string message);
    void OpenUrl(string url);
    void OpenFolder(string path);
}

public sealed class WindowsDialogs : IUserDialogs
{
    public string? File(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    public string? Folder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
    public string? Save(string title, string filter, string name)
    {
        var dialog = new SaveFileDialog { Title = title, Filter = filter, FileName = name };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    public bool Confirm(string message) => MessageBox.Show(message, "Kratos Legacy Collection", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
    public void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo))
            throw new Core.UserError("Only HTTPS web links are supported.");
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
    public void OpenFolder(string path)
    {
        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        start.ArgumentList.Add(path);
        Process.Start(start);
    }
}
