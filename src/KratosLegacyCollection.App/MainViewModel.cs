using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using KratosLegacyCollection.Core;

namespace KratosLegacyCollection.App;

public sealed class GameViewModel(GameDefinition definition, GameSettings settings, Func<GameStatus> status) : Observable
{
    public GameDefinition Definition => definition;
    public GameSettings Settings => settings;
    public string Name => definition.Name;
    public string Subtitle => definition.Subtitle;
    public string Chapter => definition.Chapter;
    public string Platform => definition.Platform;
    public string Status => LaunchService.Label(status());
    public string PrimaryAction => status() == GameStatus.ReadyToPlay ? "PLAY" : "ADD GAME FILES";
    public string Path { get => settings.Path ?? ""; set { settings.Path = value; settings.IdentityConfirmed = false; Changed(); } }
    public string GameId { get => settings.GameId ?? ""; set { settings.GameId = Catalog.NormalizeId(value); settings.IdentityConfirmed = false; Changed(); } }
    public string Revision { get => settings.Revision ?? ""; set { settings.Revision = value; Changed(); } }
    public string Crc { get => settings.Crc ?? ""; set { settings.Crc = value.Trim().ToUpperInvariant(); Changed(); } }
    public string Region => settings.GameId == null ? "Unknown" : Catalog.Region(settings.GameId);
    public int Resolution { get => settings.ResolutionScale; set { settings.ResolutionScale = value; Changed(); } }
    public string Renderer { get => settings.Renderer; set { settings.Renderer = value; Changed(); } }
    public bool Widescreen { get => settings.Widescreen; set { settings.Widescreen = value; Changed(); } }
    public GraphicsPreset Preset { get => settings.Preset; set { GraphicsProfiles.Select(settings, value); Changed(); Changed(nameof(Resolution)); Changed(nameof(Renderer)); } }
    public void Refresh() { foreach (var name in new[] { nameof(Status), nameof(PrimaryAction), nameof(Path), nameof(GameId), nameof(Region), nameof(Revision), nameof(Crc), nameof(Preset), nameof(Resolution), nameof(Renderer), nameof(Widescreen) }) Changed(name); }
}

public sealed class EmulatorViewModel(EmulatorKind kind, EmulatorSettings settings) : Observable
{
    public EmulatorKind Kind => kind;
    public string Name => kind.ToString();
    public EmulatorSettings Settings => settings;
    public string Platform => kind switch { EmulatorKind.PCSX2 => "PLAYSTATION 2", EmulatorKind.PPSSPP => "PLAYSTATION PORTABLE", _ => "PLAYSTATION 3" };
    public string Executable { get => settings.Executable ?? ""; set { settings.Executable = value; settings.Prepared = false; Changed(); Changed(nameof(Prepared)); } }
    public string DataDirectory { get => settings.DataDirectory ?? ""; set { settings.DataDirectory = value; settings.Prepared = false; Changed(); Changed(nameof(Prepared)); } }
    public bool Prepared { get => settings.Prepared; set { settings.Prepared = value; Changed(); } }
    public string Version => settings.Version is { Length: > 0 } v ? "Detected version: " + v : "Version unavailable";
    public string Guidance => kind switch {
        EmulatorKind.PCSX2 => "Open PCSX2. Complete its wizard with your own BIOS. Review Settings > Graphics, Controllers, Memory Cards, Folders and Game List. Use game properties for built-in widescreen patches. Save states are version-sensitive; memory cards are your primary saves.",
        EmulatorKind.PPSSPP => "Open PPSSPP. Check Settings > Graphics and Controls. Confirm the memory stick location in Settings > System. The data folder below must contain PSP/SYSTEM, PSP/SAVEDATA and PSP/TEXTURES. Enable Replace textures for your own packs.",
        _ => "Open RPCS3. Follow Quickstart to install firmware from PlayStation.com using File > Install Firmware. Configure Pads, add your disc folders and review each game's compatibility page. Manage > Game Patches applies patches by PPU hash and version."
    };
    public void Refresh() { Changed(nameof(Executable)); Changed(nameof(DataDirectory)); Changed(nameof(Version)); Changed(nameof(Prepared)); }
}

public sealed class MainViewModel : Observable
{
    public CollectionPaths Paths { get; }
    public LauncherSettings State { get; private set; }
    private readonly IUserDialogs dialogs;
    private readonly SettingsStore store;
    private readonly LaunchService launcher;
    private readonly EnhancementManager mods;
    private readonly DiagnosticLog log;
    public ObservableCollection<GameViewModel> Games { get; } = [];
    public ObservableCollection<EmulatorViewModel> Emulators { get; } = [];
    public ObservableCollection<InstalledEnhancement> Installed { get; } = [];
    public ObservableCollection<GameInspection> Discovered { get; } = [];
    private GameViewModel selected = null!;
    public GameViewModel Selected { get => selected; set { if (Set(ref selected, value) && value != null) RefreshMods(); } }
    public InstalledEnhancement? SelectedMod { get; set; }
    public GameInspection? SelectedDiscovery { get; set; }
    private string page = "GAME LIBRARY";
    public string Page { get => page; set { if (Set(ref page, value)) { Changed(nameof(IsLibrary)); Changed(nameof(IsEnhancements)); Changed(nameof(IsEmulators)); Changed(nameof(IsSettings)); Changed(nameof(IsAbout)); } } }
    public bool IsLibrary => Page == "GAME LIBRARY";
    public bool IsEnhancements => Page == "ENHANCEMENTS";
    public bool IsEmulators => Page == "EMULATOR SETUP";
    public bool IsSettings => Page == "SETTINGS";
    public bool IsAbout => Page == "ABOUT";
    private bool busy;
    public bool Busy { get => busy; private set { Set(ref busy, value); Changed(nameof(Idle)); CommandManager.InvalidateRequerySuggested(); } }
    public bool Idle => !Busy;
    private string message = "Your collection. Your files. One place to play.";
    public string Message { get => message; private set => Set(ref message, value); }
    private bool error;
    public bool HasError { get => error; private set => Set(ref error, value); }
    public bool SetupVisible { get => !State.SetupComplete; }
    public int SetupStep => State.SetupStep;
    public string SetupProgress => $"STEP {State.SetupStep + 1} OF 5";
    public bool WelcomeStep => State.SetupStep == 0;
    public bool EmulatorStep => State.SetupStep == 1;
    public bool ConfigurationStep => State.SetupStep == 2;
    public bool ModeStep => State.SetupStep == 3;
    public bool FinishStep => State.SetupStep == 4;
    public string NextLabel => WelcomeStep ? "GET STARTED" : FinishStep ? "ENTER THE PANTHEON" : "SAVE AND CONTINUE";
    public string SetupTitle => State.SetupStep switch { 0 => "WELCOME TO\nKRATOS LEGACY COLLECTION", 1 => "PREPARE YOUR EMULATORS", 2 => "MAKE IT YOURS", 3 => "CHOOSE YOUR INSTALLATION", _ => "SETUP COMPLETE" };
    public bool Portable { get => State.PortableMode; set { State.PortableMode = value; Changed(); Changed(nameof(StorageDescription)); } }
    public string StorageDescription => Portable ? "PORTABLE MODE: collection files use relative paths whenever they are inside this collection. External emulator paths remain external. Create portable copies below when needed." : "USE EXISTING EMULATORS: keep games and emulator data where they are. No original files are copied or moved automatically.";
    public string StorageRoot => Paths.Root;
    public bool Fullscreen { get => State.Fullscreen; set { State.Fullscreen = value; Changed(); } }
    public bool ReducedMotion { get => State.ReducedMotion; set { State.ReducedMotion = value; Changed(); } }
    public string CollectionSummary => $"{Games.Count(g => g.Status == "READY TO PLAY")} OF {Games.Count} READY";
    public string CreatorDiscord { get => State.CreatorDiscord ?? ""; set { State.CreatorDiscord = value; Changed(); } }
    public string CreatorSource { get => State.CreatorDiscordSource ?? ""; set { State.CreatorDiscordSource = value; Changed(); } }
    public string ModAuthor { get; set; } = "User supplied";
    public IReadOnlyList<GraphicsPreset> Presets { get; } = Enum.GetValues<GraphicsPreset>();
    public IReadOnlyList<string> Renderers { get; } = ["Default", "Vulkan", "Direct3D 11", "OpenGL"];
    public ICommand Navigate { get; }
    public ICommand Next { get; }
    public ICommand Back { get; }
    public ICommand ResumeSetup { get; }
    public ICommand SaveSettings { get; }
    public ICommand DetectEmulators { get; }
    public ICommand SearchEmulators { get; }
    public ICommand BrowseEmulator { get; }
    public ICommand BrowseData { get; }
    public ICommand OpenEmulator { get; }
    public ICommand DownloadEmulator { get; }
    public ICommand OpenGuide { get; }
    public ICommand CreatePortable { get; }
    public ICommand BrowseGame { get; }
    public ICommand BrowseGameFolder { get; }
    public ICommand ValidateGame { get; }
    public ICommand SearchGames { get; }
    public ICommand AssociateGame { get; }
    public ICommand CopyGame { get; }
    public ICommand Play { get; }
    public ICommand CardAction { get; }
    public ICommand ImportTextures { get; }
    public ICommand ImportPerformance { get; }
    public ICommand ImportDisplay { get; }
    public ICommand ModAction { get; }
    public ICommand OpenCreator { get; }
    public ICommand OpenCreatorPage { get; }
    public ICommand Backup { get; }
    public ICommand Restore { get; }
    public ICommand OpenData { get; }
    public ICommand Cancel { get; }
    private CancellationTokenSource? cancellation;

    public MainViewModel(CollectionPaths paths, LauncherSettings state, IUserDialogs dialogs) {
        Paths = paths; State = state; this.dialogs = dialogs; store = new(paths); launcher = new(paths); mods = new(paths); log = new(paths);
        BuildEntries();
        ICommand Command(Func<object?, Task> action) => new ActionCommand(async p => await Run(() => action(p)), () => !Busy);
        Navigate = Command(p => { Page = (string)p!; return Task.CompletedTask; });
        SaveSettings = Command(_ => Save("Settings saved."));
        Next = Command(async _ => { if (State.SetupStep == 4) State.SetupComplete = true; else State.SetupStep++; await Save("Setup progress saved."); RefreshSetup(); });
        Back = Command(async _ => { State.SetupStep = Math.Max(0, State.SetupStep - 1); await Save("Setup progress saved."); RefreshSetup(); });
        ResumeSetup = Command(async _ => { State.SetupComplete = false; State.SetupStep = 0; await Save("Setup reopened."); RefreshSetup(); });
        DetectEmulators = Command(_ => Detect(EmulatorDiscovery.UsualRoots()));
        SearchEmulators = Command(async _ => { var root = dialogs.Folder("Search this folder for emulators"); if (root != null) await Detect([root]); });
        BrowseEmulator = Command(async p => { var vm = (EmulatorViewModel)p!; var file = dialogs.File($"Select {vm.Name}", "Windows executable|*.exe"); if (file != null) { SetEmulator(vm, EmulatorDiscovery.Validate(vm.Kind, file)); await Save("Emulator selected. Complete its configuration before marking it ready."); } });
        BrowseData = Command(async p => { var vm = (EmulatorViewModel)p!; var folder = dialogs.Folder("Select the emulator data folder"); if (folder != null) { vm.DataDirectory = Paths.Store(folder); await Save("Data folder saved. Review your emulator's folder settings."); } });
        OpenEmulator = Command(p => { var vm = p as EmulatorViewModel ?? Emulators.Single(e => e.Kind == Selected.Definition.Emulator); if (string.IsNullOrEmpty(vm.Executable)) throw new UserError("Select an emulator executable first."); var candidate = EmulatorDiscovery.Validate(vm.Kind, Paths.Resolve(vm.Executable)); using var process = LaunchService.Start(new(candidate.Executable, System.IO.Path.GetDirectoryName(candidate.Executable)!, vm.Settings.PortableCopy && vm.Kind == EmulatorKind.PCSX2 ? ["-portable"] : [])); Message = "Emulator opened. Complete its setup, then mark it configured here."; return Task.CompletedTask; });
        DownloadEmulator = Command(p => { dialogs.OpenUrl(EmulatorDiscovery.DownloadPages[((EmulatorViewModel)p!).Kind]); return Task.CompletedTask; });
        OpenGuide = Command(p => { var kind = p is EmulatorViewModel vm ? vm.Kind : Selected.Definition.Emulator; dialogs.OpenUrl(kind switch { EmulatorKind.PCSX2 => "https://pcsx2.net/docs/setup/", EmulatorKind.PPSSPP => "https://www.ppsspp.org/docs/", _ => "https://rpcs3.net/quickstart" }); return Task.CompletedTask; });
        CreatePortable = Command(async p => { var vm = (EmulatorViewModel)p!; var root = dialogs.Folder("Choose a parent folder for a new portable emulator copy"); if (root == null) return; if (!dialogs.Confirm("Copy this emulator and its configured data into a new folder? Originals stay in place. BIOS paths, hardware and controller bindings must be checked in the copied emulator.")) return; var target = System.IO.Path.Combine(root, vm.Name + "-portable"); var prepared = await new PortablePreparation(Paths).PrepareAsync(vm.Kind, vm.Settings, target, Progress(), Token); SetEmulator(vm, prepared); vm.Settings.PortableCopy = true; await Save("Portable copy verified. Open it to check BIOS, folders and controls before marking it ready."); });
        BrowseGame = Command(async _ => { var file = dialogs.File("Select your own game image", "Game images|*.iso;*.cso;*.chd"); if (file != null) await Attach(file); });
        BrowseGameFolder = Command(async _ => { var folder = dialogs.Folder("Select your own PS3 disc folder"); if (folder != null) await Attach(folder); });
        ValidateGame = Command(async _ => await Attach(Paths.Resolve(Selected.Path), true));
        SearchGames = Command(async _ => { var root = dialogs.Folder("Search this folder for your games"); if (root == null) return; Discovered.Clear(); foreach (var match in await new GameDiscovery(new()).FindAsync(root, Progress(), Token)) Discovered.Add(match); Message = Discovered.Count == 0 ? "No supported saga games were identified in this folder." : $"Found {Discovered.Count} game images. Select a result and associate it."; });
        AssociateGame = Command(async _ => { if (SelectedDiscovery == null) throw new UserError("Select a search result first."); var definition = Catalog.Find(SelectedDiscovery.GameId!); if (definition == null) throw new UserError("This game is not in the library."); Selected = Games.Single(g => g.Definition.Key == definition.Key); await Attach(SelectedDiscovery.Path); });
        CopyGame = Command(async _ => { if (Selected.Settings.Path == null || !Selected.Settings.IdentityConfirmed) throw new UserError("Validate the game first."); var folder = dialogs.Folder("Select a parent folder for a verified game copy"); if (folder == null) return; var original = Paths.Resolve(Selected.Settings.Path); var target = System.IO.Path.Combine(folder, Selected.Definition.Key + (Directory.Exists(original) ? "" : System.IO.Path.GetExtension(original))); await StorageOperations.CopyVerifiedAsync(original, target, Progress(), Token); await Attach(target); Message = "Copy verified with SHA-256. The original was preserved."; });
        Play = Command(async _ => await Launch());
        CardAction = Command(async p => { Selected = (GameViewModel)p!; if (launcher.Status(Selected.Definition, State) == GameStatus.ReadyToPlay) await Launch(); else { var path = Selected.Definition.Emulator == EmulatorKind.RPCS3 ? dialogs.Folder("Select your own PS3 disc folder") : dialogs.File("Select your own game image", "Game images|*.iso;*.cso;*.chd"); if (path != null) await Attach(path); } });
        ImportTextures = Command(async p => { var file = p as string ?? dialogs.File("Import a texture pack you are allowed to use", "ZIP texture packs|*.zip"); if (file != null) await ImportTexture(file); });
        ImportPerformance = Command(_ => ImportPatch(EnhancementKind.Performance));
        ImportDisplay = Command(_ => ImportPatch(EnhancementKind.Display));
        ModAction = Command(async p => await ChangeMod((string)p!));
        OpenCreatorPage = Command(_ => { dialogs.OpenUrl("https://www.youtube.com/@modderespartano1073"); return Task.CompletedTask; });
        OpenCreator = Command(async _ => { if (!Uri.TryCreate(CreatorDiscord, UriKind.Absolute, out var link) || link.Scheme != "https" || link.Host is not ("discord.gg" or "discord.com") || link.AbsolutePath.Length < 2 || !Uri.TryCreate(CreatorSource, UriKind.Absolute, out var source) || source.Scheme != "https") throw new UserError("No official Discord invitation has been independently verified. Copy the current invitation and original creator publication into Settings after checking the creator's own channel."); if (!dialogs.Confirm("Open this user-verified invitation? Confirm it is linked from the creator's original publication. The launcher has not authenticated this invitation.")) return; dialogs.OpenUrl(link.AbsoluteUri); await Save("Creator invitation opened in your browser."); });
        Backup = Command(async _ => { var target = dialogs.Save("Back up launcher settings", "ZIP backup|*.zip", "kratos-settings-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip"); if (target != null) { await new ConfigurationBackup(Paths).ExportAsync(target, State, Token); Message = "Settings backup created. Game files, emulator saves and mod payloads are separate."; } });
        Restore = Command(async _ => { var source = dialogs.File("Restore launcher settings", "ZIP backup|*.zip"); if (source == null) return; await new ConfigurationBackup(Paths).ReadAsync(source, Token); if (!dialogs.Confirm("Restore launcher settings? A backup of the current settings will be saved first. Game files, mods and emulator saves are unaffected.")) return; await new ConfigurationBackup(Paths).RestoreAsync(source, Token); State = await store.LoadAsync(Token); BuildEntries(); RefreshSetup(); Changed(nameof(Portable)); Changed(nameof(Fullscreen)); Changed(nameof(ReducedMotion)); Message = "Launcher settings restored."; });
        OpenData = Command(_ => { Directory.CreateDirectory(Paths.Data); dialogs.OpenFolder(Paths.Data); return Task.CompletedTask; });
        Cancel = new ActionCommand(_ => cancellation?.Cancel(), () => Busy);
    }
    private CancellationToken Token => cancellation?.Token ?? CancellationToken.None;
    private IProgress<string> Progress() => new Progress<string>(text => Message = text);
    private void BuildEntries() {
        Games.Clear(); Emulators.Clear();
        foreach (var game in Catalog.Games) Games.Add(new(game, State.Games[game.Key], () => launcher.Status(game, State)));
        foreach (var kind in Enum.GetValues<EmulatorKind>()) Emulators.Add(new(kind, State.Emulators[kind]));
        Selected = Games[0];
    }
    private async Task Run(Func<Task> operation) {
        if (Busy) return; Busy = true; HasError = false; cancellation = new();
        try { await operation(); Refresh(); }
        catch (OperationCanceledException) { Message = "Operation cancelled. Original files were preserved."; }
        catch (Exception ex) { log.Write("Operation failed", ex); HasError = true; Message = ex is UserError ? ex.Message : "The operation failed. Check available storage, file permissions and connected drives. See Data/Logs for details."; }
        finally { cancellation.Dispose(); cancellation = null; Busy = false; Refresh(); }
    }
    private async Task Save(string text) { await store.SaveAsync(State, Token); Message = text; }
    private void Refresh() { foreach (var game in Games) game.Refresh(); Changed(nameof(CollectionSummary)); RefreshMods(); }
    private void RefreshMods() { Installed.Clear(); if (selected != null) foreach (var mod in mods.Load().Where(m => m.GameKey == selected.Definition.Key)) Installed.Add(mod); }
    private void RefreshSetup() { foreach (var name in new[] { nameof(SetupVisible), nameof(SetupStep), nameof(SetupProgress), nameof(WelcomeStep), nameof(EmulatorStep), nameof(ConfigurationStep), nameof(ModeStep), nameof(FinishStep), nameof(SetupTitle), nameof(NextLabel) }) Changed(name); }
    private void SetEmulator(EmulatorViewModel vm, EmulatorCandidate candidate) { vm.Settings.Executable = Paths.Store(candidate.Executable); vm.Settings.Version = candidate.Version; vm.Settings.DataDirectory = candidate.DataDirectory == null ? null : Paths.Store(candidate.DataDirectory); vm.Settings.Prepared = false; vm.Refresh(); }
    private async Task Detect(IEnumerable<string> roots) {
        var candidates = await new EmulatorDiscovery().FindAsync(roots, Progress(), Token);
        foreach (var vm in Emulators.Where(e => string.IsNullOrEmpty(e.Executable))) { var found = candidates.FirstOrDefault(c => c.Kind == vm.Kind); if (found != null) SetEmulator(vm, found); }
        await Save($"Found {candidates.Count} emulator executables. Existing selections were preserved. Browse to choose a different installation.");
    }
    private async Task Attach(string path, bool allowManual = false) {
        var metadata = await new GameInspector().InspectAsync(path, Token);
        if (!metadata.Supported) throw new UserError(metadata.Message);
        var game = Selected.Definition; var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        if (game.Emulator == EmulatorKind.RPCS3 && !Directory.Exists(path) || game.Emulator != EmulatorKind.RPCS3 && Directory.Exists(path) || game.Emulator != EmulatorKind.PPSSPP && extension == ".cso" || game.Emulator != EmulatorKind.PCSX2 && extension == ".chd") throw new UserError("This format does not match the selected game's emulator.");
        var id = metadata.GameId;
        if (id == null && allowManual) { id = Selected.GameId; if (!dialogs.Confirm("Confirm the Game ID was read from this exact compressed image in the emulator. Incorrect identity can make patches unsafe.")) return; }
        if (id == null) { Selected.Path = Paths.Store(path); await Save(metadata.Message + " Enter the Game ID and choose VALIDATE FILES."); return; }
        if (!game.GameIds.Contains(id)) throw new UserError("The detected Game ID does not match this title or a supported region. Original files were preserved.");
        Selected.Settings.Path = Paths.Store(path); Selected.Settings.GameId = id; Selected.Settings.Revision = metadata.Revision ?? Selected.Settings.Revision; Selected.Settings.IdentityConfirmed = true;
        await Save("Game identity verified. " + metadata.Message);
    }
    private void EnsureClosed() {
        var names = EmulatorDiscovery.Names[Selected.Definition.Emulator].Select(System.IO.Path.GetFileNameWithoutExtension);
        foreach (var name in names) if (Process.GetProcessesByName(name!).Length > 0) throw new UserError("Close this emulator before changing enhancements or starting another game.");
    }
    private async Task Launch() {
        if (launcher.Status(Selected.Definition, State) != GameStatus.ReadyToPlay) throw new UserError("This game is not ready. Validate its files, select the emulator and complete its configuration.");
        EnsureClosed();
        var inspected = await new GameInspector().InspectAsync(Paths.Resolve(Selected.Settings.Path!), Token);
        if (!inspected.Supported || inspected.GameId != null && inspected.GameId != Selected.GameId) throw new UserError("The game files changed. Validate them again before launching.");
        await store.SaveAsync(State, Token);
        var active = mods.Load().Where(m => m.GameKey == Selected.Definition.Key && m.Enabled).ToArray();
        var profile = await new EmulatorProfiles(Paths).PrepareAsync(Selected.Definition, Selected.Settings, State.Emulators[Selected.Definition.Emulator], active.Any(m => m.Kind == EnhancementKind.Textures), active.Any(m => m.Kind != EnhancementKind.Textures), Token);
        var plan = launcher.Plan(Selected.Definition, State, profile);
        Message = "LAUNCHING GAME: " + Selected.Name;
        using var process = LaunchService.Start(plan); log.Write("Emulator process started: " + Selected.Definition.Key);
        var exited = process.WaitForExitAsync();
        if (await Task.WhenAny(exited, Task.Delay(1800, Token)) == exited && process.ExitCode != 0) throw new UserError("The emulator exited with an error. Open it directly and review BIOS, firmware and game compatibility.");
        Message = "Game launch sent to " + Selected.Definition.Emulator + ".";
    }
    private string Target(EnhancementKind kind) {
        var emulator = State.Emulators[Selected.Definition.Emulator];
        if (emulator.DataDirectory == null) throw new UserError("Select the emulator's actual data directory first.");
        var data = Paths.Resolve(emulator.DataDirectory);
        if (!Directory.Exists(data)) throw new UserError("The emulator data directory is unavailable.");
        return Selected.Definition.Emulator switch {
            EmulatorKind.PCSX2 => System.IO.Path.Combine(data, kind == EnhancementKind.Textures ? "textures" : "cheats", kind == EnhancementKind.Textures ? Selected.GameId.Insert(4, "-") : ""),
            EmulatorKind.PPSSPP => System.IO.Path.Combine(data, "PSP", kind == EnhancementKind.Textures ? "TEXTURES" : "Cheats", kind == EnhancementKind.Textures ? Selected.GameId : ""),
            _ => throw new UserError("RPCS3 enhancements use its native patch manager. Texture injection is not supported.")
        };
    }
    private async Task ImportTexture(string file) {
        if (!Selected.Settings.IdentityConfirmed) throw new UserError("Validate this game's identity first.");
        var pack = await Task.Run(() => new TexturePackInspector().Inspect(file, Selected.Definition, Selected.GameId), Token);
        var target = Target(EnhancementKind.Textures);
        if (!dialogs.Confirm($"Import {pack.FileCount} files ({pack.ExpandedBytes / 1048576.0:F1} MB) using {pack.Profile}? The pack will be stored privately. Choose ENABLE afterwards to install it into the emulator.")) return;
        await mods.ImportTexturesAsync(pack, Selected.Definition, target, ModAuthor, Token); Message = "Texture pack imported. Select it below and choose ENABLE.";
    }
    private async Task ImportPatch(EnhancementKind kind) {
        if (!Selected.Settings.IdentityConfirmed) throw new UserError("Validate this game's identity first.");
        var file = dialogs.File("Import a patch from an authorized source", "Emulator patches|*.pnach;*.ini"); if (file == null) return;
        if (!dialogs.Confirm("Confirm this patch is compatible with the exact Game ID, revision and emulator version. Frame-rate patches may change physics, animations, audio timing and cutscenes. The launcher checks identity markers but cannot prove a patch's behavior.")) return;
        await mods.ImportPatchAsync(file, Selected.Definition, Selected.Settings, Target(kind), kind, Token); Message = "Patch imported and disabled. Select it below to enable it.";
    }
    private async Task ChangeMod(string action) {
        var mod = SelectedMod ?? throw new UserError("Select an installed enhancement first."); EnsureClosed();
        if (mod.GameId != Selected.GameId) throw new UserError("This enhancement belongs to a different game version.");
        if (action == "page") { if (mod.Page == null) throw new UserError("No source page was supplied with this local package."); dialogs.OpenUrl(mod.Page); return; }
        if (action == "enable") { if (!dialogs.Confirm("Enable this enhancement in the configured emulator folder? Existing files will be backed up before replacement. Close the emulator first.")) return; await Task.Run(() => mods.EnableAsync(mod, true, Token), Token); Message = "Enhancement enabled. Previous files are backed up."; }
        else if (action == "disable") { await Task.Run(() => mods.DisableAsync(mod), Token); Message = "Enhancement disabled. Previous files restored."; }
        else if (action == "uninstall") { if (!dialogs.Confirm("Uninstall this enhancement and its private payload? Previous emulator files will be restored first.")) return; await Task.Run(() => mods.UninstallAsync(mod), Token); Message = "Enhancement uninstalled. Saves and unrelated mods were preserved."; }
    }
}
