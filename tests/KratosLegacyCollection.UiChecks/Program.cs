using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KratosLegacyCollection.App;
using KratosLegacyCollection.Core;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var root = Path.GetFullPath(Path.Combine("artifacts", "ui-checks", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        var app = new App();
        app.InitializeComponent();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var dialogs = new TestDialogs();
        var state = LauncherSettings.Create();
        state.ReducedMotion = true;
        var paths = new CollectionPaths(root);
        var vm = new MainViewModel(paths, state, dialogs);
        var window = new MainWindow(vm);
        var evidence = new List<string>();
        var trace = new BindingTrace();
        PresentationTraceSources.DataBindingSource.Listeners.Add(trace);
        void Check(string name, bool condition)
        {
            if (!condition)
                throw new Exception(name);
            evidence.Add("PASS " + name);
            Console.WriteLine(evidence[^1]);
        }
        void Invoke(ICommand command, object? parameter = null)
        {
            Check("Command is available", command.CanExecute(parameter));
            command.Execute(parameter);
            var watch = Stopwatch.StartNew();
            while (vm.Busy && watch.Elapsed < TimeSpan.FromSeconds(20))
                Pump();
            Pump();
            if (vm.Busy)
                throw new Exception("Command timed out");
        }
        void Capture(string name)
        {
            window.UpdateLayout();
            Pump();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var output = Path.Combine(root, name + ".png");
            using var file = File.Create(output);
            encoder.Save(file);
        }
        try
        {
            window.Show();
            Pump();
            Check("Initial setup is visible and contains no marketing status", vm.SetupVisible && vm.Message == "");
            var next = (Button)window.FindName("SetupNext");
            Check("Logical keyboard focus starts inside setup", FocusManager.GetFocusedElement(FocusManager.GetFocusScope(next)) == next);
            Capture("welcome");
            Invoke(vm.Next);
            Check("Step 2 saves progress", vm.SetupStep == 1 && File.Exists(new SettingsStore(paths).FilePath));
            Invoke(vm.Next);
            Check("Configuration heading uses requested copy", vm.SetupTitle == "CONFIGURE EMULATORS");
            Invoke(vm.Back);
            Check("Back returns to emulator setup", vm.SetupStep == 1);
            Invoke(vm.Next);
            Invoke(vm.Next);
            Check("Installation mode does not require emulators", vm.ModeStep);
            Invoke(vm.Next);
            Check("Finish state", vm.FinishStep);
            Invoke(vm.Next);
            Check("Empty library opens independently", !vm.SetupVisible && vm.Games.Count == 6 && vm.CollectionSummary == "0 OF 6 READY");
            var selectedGame = vm.Selected;
            Check("PCSX2 hides unsupported renderer options", !vm.ShowRenderer);
            vm.Selected = vm.Games.Single(g => g.Definition.Key == "gow3");
            Check("RPCS3 renderer list contains only supported backends", vm.ShowRenderer && !vm.Renderers.Contains("Direct3D 11"));
            vm.Selected = selectedGame;
            Invoke(vm.SortChronologically);
            Check("Chronological sort preserves selection and game settings", vm.Games.Select(g => g.Definition.Key).SequenceEqual(new[] { "ascension", "chains", "gow1", "ghost", "gow2", "gow3" }) && ReferenceEquals(vm.Selected, selectedGame));
            Invoke(vm.SortByRelease);
            Check("Release sort follows the requested sequence", vm.Games.Select(g => g.Definition.Key).SequenceEqual(new[] { "gow1", "gow2", "chains", "ghost", "gow3", "ascension" }));
            foreach (var page in new[] { "GAME LIBRARY", "ENHANCEMENTS", "EMULATOR SETUP", "SETTINGS", "ABOUT" })
            {
                Invoke(vm.Navigate, page);
                Check("Navigation destination " + page, vm.Page == page);
            }
            Invoke(vm.Navigate, "GAME LIBRARY");
            foreach (var size in new[] { (900, 650), (1280, 860), (1600, 900) })
            {
                window.Width = size.Item1;
                window.Height = size.Item2;
                window.UpdateLayout();
                Pump();
                Capture("library-" + size.Item1);
                var scroll = Descendants<ScrollViewer>(window).First(s => s.IsVisible);
                Check("No horizontal content overflow at " + size.Item1, scroll.ExtentWidth <= scroll.ViewportWidth + 1);
            }
            foreach (var game in vm.Games)
            {
                vm.Selected = game;
                Check("Independent empty status " + game.Name, game.Status == "GAME FILES REQUIRED");
                Invoke(vm.Play);
                Check("Missing game produces visible English error", vm.HasError && vm.Message.Contains("not ready"));
            }
            var artworkPages = new Dictionary<string, string> { ["gow1"] = "36454", ["gow2"] = "35531", ["chains"] = "36385", ["ghost"] = "4561", ["gow3"] = "38217", ["ascension"] = "5248417" };
            foreach (var game in vm.Games)
            {
                vm.Selected = game;
                Invoke(vm.OpenArtworkPage);
                Check("SteamGridDB page for " + game.Name, dialogs.LastUrl == "https://www.steamgriddb.com/game/" + artworkPages[game.Definition.Key]);
            }
            Invoke(vm.LoadArtwork);
            Check("Artwork extraction without files is handled", vm.HasError);
            Invoke(vm.ChooseArtwork);
            Check("Artwork manual picker route", dialogs.LastRequest == "Choose game artwork");
            var artworkFile = Path.Combine(root, "generated-artwork.png");
            var generatedImage = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 200, 255, 0, 0, 200, 255, 0, 0, 200, 255, 0, 0, 200, 255 }, 8);
            var artworkEncoder = new PngBitmapEncoder();
            artworkEncoder.Frames.Add(BitmapFrame.Create(generatedImage));
            using (var file = File.Create(artworkFile))
                artworkEncoder.Save(file);
            var artworkHash = StorageOperations.Hash(artworkFile);
            dialogs.NextFile = artworkFile;
            Invoke(vm.ChooseArtwork);
            Check("Manual artwork loads, persists and preserves the source image", vm.Selected.Artwork != null && vm.Selected.Settings.ArtworkPath!.StartsWith("./Data/Artwork/") && StorageOperations.Hash(artworkFile) == artworkHash);
            Invoke(vm.RemoveArtwork);
            Check("Artwork removal preserves original files", vm.Selected.Artwork == null);
            vm.Selected = vm.Games[0];
            Invoke(vm.BrowseGame);
            Check("Game image picker route", dialogs.LastRequest.Contains("game image"));
            Invoke(vm.BrowseGameFolder);
            Check("PS3 folder picker route", dialogs.LastRequest.Contains("PS3 disc"));
            Invoke(vm.SearchGames);
            Check("Game search picker route", dialogs.LastRequest.Contains("your games"));
            Invoke(vm.AssociateGame);
            Check("No selected discovery validation", vm.HasError);
            Invoke(vm.CopyGame);
            Check("Unvalidated portable copy rejected", vm.HasError);
            Invoke(vm.ImportTextures);
            Check("Texture archive picker route", dialogs.LastRequest.Contains("texture pack"));
            Invoke(vm.ImportPerformance);
            Check("Unvalidated performance patch rejected", vm.HasError);
            Invoke(vm.ImportDisplay);
            Check("Unvalidated display patch rejected", vm.HasError);
            Check("Unavailable patch controls are explicit", !vm.HasPerformancePatch && !vm.HasDisplayPatch && vm.PerformanceStatus.StartsWith("NOT AVAILABLE"));
            Invoke(vm.ModAction, "enable");
            Check("No selected enhancement validation", vm.HasError);
            foreach (var emulator in vm.Emulators)
            {
                Invoke(vm.BrowseEmulator, emulator);
                Check("Executable picker " + emulator.Name, dialogs.LastRequest.Contains(emulator.Name));
                Invoke(vm.BrowseData, emulator);
                Check("Data folder picker " + emulator.Name, dialogs.LastRequest.Contains("data folder"));
                Invoke(vm.DownloadEmulator, emulator);
                Check("Official HTTPS download " + emulator.Name, dialogs.LastUrl == EmulatorDiscovery.DownloadPages[emulator.Kind]);
                Invoke(vm.OpenGuide, emulator);
                Check("Official guide " + emulator.Name, dialogs.LastUrl.StartsWith("https://"));
                Invoke(vm.OpenEmulator, emulator);
                Check("Missing executable is handled " + emulator.Name, vm.HasError);
            }
            Invoke(vm.OpenCreator);
            Check("Verified creator Discord route", !vm.HasError && dialogs.LastUrl == EnhancementSources.CreatorDiscord);
            vm.CreatorDiscord = "https://example.org/unverified";
            Invoke(vm.OpenCreator);
            Check("Invalid Discord host is rejected", vm.HasError);
            Invoke(vm.OpenCreatorPage);
            Check("Creator page route", dialogs.LastUrl == EnhancementSources.CreatorChannel);
            vm.Selected.PresetLabel = "ULTRA HD";
            Check("Preset enables a real per-game profile", vm.Selected.Resolution == 6 && vm.Selected.UseLaunchProfile);
            Invoke(vm.SaveSettings);
            Check("Settings persistence command", !vm.HasError && vm.Message == "Settings saved.");
            Invoke(vm.Backup);
            Check("Backup save picker route", dialogs.LastRequest.Contains("Back up"));
            Invoke(vm.Restore);
            Check("Restore picker route", dialogs.LastRequest.Contains("Restore"));
            Invoke(vm.OpenData);
            Check("Data folder route", dialogs.LastFolder == paths.Data);
            Invoke(vm.ResumeSetup);
            Check("Setup can be reopened", vm.WelcomeStep && vm.SetupVisible);
            Check("All WPF data bindings resolve", trace.Errors.Count == 0);
            Check("All interactive buttons are bound", Descendants<Button>(window).Where(b => b.IsVisible).All(b => b.Command != null));
            File.WriteAllLines(Path.Combine(root, "results.txt"), evidence);
            Console.WriteLine("UI EVIDENCE " + root);
            if (args.Length == 2 && args[0] == "--screenshots")
            {
                Directory.CreateDirectory(args[1]);
                foreach (var file in Directory.GetFiles(root, "*.png").Where(f => Path.GetFileName(f) != "generated-artwork.png"))
                    File.Copy(file, Path.Combine(args[1], Path.GetFileName(file)), true);
            }
            window.Close();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); foreach (var item in trace.Errors) Console.Error.WriteLine(item); window.Close(); return 1; }
    }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        Thread.Sleep(8);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                yield return match;
            foreach (var descendant in Descendants<T>(child))
                yield return descendant;
        }
    }
    private sealed class BindingTrace : TraceListener
    {
        public List<string> Errors { get; } = []; public override void Write(string? message)
        {
            if (message?.Contains("Error:") == true)
                Errors.Add(message);
        }
        public override void WriteLine(string? message) => Write(message);
    }
    private sealed class TestDialogs : IUserDialogs
    {
        public string? NextFile
        {
            get; set;
        }
        public string LastRequest { get; private set; } = ""; public string LastUrl { get; private set; } = ""; public string LastFolder { get; private set; } = "";
        public string? File(string title, string filter)
        {
            LastRequest = title;
            var file = NextFile;
            NextFile = null;
            return file;
        }
        public string? Folder(string title)
        {
            LastRequest = title;
            return null;
        }
        public string? Save(string title, string filter, string name)
        {
            LastRequest = title;
            return null;
        }
        public bool Confirm(string message) => false;
        public void OpenUrl(string url) => LastUrl = url;
        public void OpenFolder(string path) => LastFolder = path;
    }
}
