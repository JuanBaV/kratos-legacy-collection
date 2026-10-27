using System.Globalization;
using System.IO;
using System.Windows;
using KratosLegacyCollection.Core;

namespace KratosLegacyCollection.App;

public partial class App : Application
{
    private FileStream? collectionLock;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        string root = AppContext.BaseDirectory;
        var index = Array.IndexOf(e.Args, "--data-root");
        if (index >= 0 && index + 1 < e.Args.Length)
            root = Path.GetFullPath(e.Args[index + 1]);
        else
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Data"));
                using var probe = File.Create(Path.Combine(root, "Data", ".write-probe"), 1, FileOptions.DeleteOnClose);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KratosLegacyCollection"); }
        }
        var paths = new CollectionPaths(root);
        try
        {
            Directory.CreateDirectory(paths.Data);
            try
            {
                collectionLock = new FileStream(Path.Combine(paths.Data, ".collection.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) { throw new UserError("This collection is already open, or its data folder is unavailable. Close the other launcher instance and try again."); }
            var state = await new SettingsStore(paths).LoadAsync();
            var vm = new MainViewModel(paths, state, new WindowsDialogs());
            var window = new MainWindow(vm);
            MainWindow = window;
            window.Show();
            DispatcherUnhandledException += (_, args) => { new DiagnosticLog(paths).Write("Unhandled UI error", args.Exception); MessageBox.Show("The operation failed. Your settings and originals were preserved. See Data/Logs for details.", "Kratos Legacy Collection", MessageBoxButton.OK, MessageBoxImage.Error); args.Handled = true; };
        }
        catch (Exception error) { new DiagnosticLog(paths).Write("Startup failed", error); MessageBox.Show(error is UserError ? error.Message : "The collection could not be opened. Check the storage location and permissions.", "Kratos Legacy Collection", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        collectionLock?.Dispose();
        base.OnExit(e);
    }
}
