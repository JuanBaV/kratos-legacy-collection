using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using KratosLegacyCollection.Core;

if (args is ["--capture-arguments", var capture, ..]) { await File.WriteAllLinesAsync(capture, args.Skip(2)); return 0; }
var results = new List<(string name, bool passed, string? error)>();
var root = Path.Combine(Environment.CurrentDirectory, "artifacts", "test-runs", "klc-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var paths = new CollectionPaths(root);
async Task Test(string name, Func<Task> action)
{
    try
    {
        await action();
        results.Add((name, true, null));
        Console.WriteLine("PASS " + name);
    }
    catch (Exception e) { results.Add((name, false, e.ToString())); Console.WriteLine("FAIL " + name + ": " + e.Message); }
}
void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
async Task Reject(Func<Task> action) { try { await action(); } catch (Exception e) when (e is UserError or IOException or UnauthorizedAccessException) { return; } throw new Exception("Expected operation to be rejected"); }
string Fixture(string name, byte[] content) { var file = Path.GetFullPath(Path.Combine(root, name)); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllBytes(file, content); return file; }
string Zip(string name, params (string file, string text)[] entries)
{
    var path = Path.Combine(root, name);
    using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
    foreach (var (file, text) in entries)
    {
        using var writer = new StreamWriter(zip.CreateEntry(file).Open());
        writer.Write(text);
    }
    return path;
}
byte[] Sfo(string id)
{
    var entries = new[] { ("DISC_ID", id), ("TITLE", "Legal generated fixture"), ("DISC_VERSION", "1.00") };
    var keys = Encoding.UTF8.GetBytes(string.Join('\0', entries.Select(e => e.Item1)) + "\0");
    var values = Encoding.UTF8.GetBytes(string.Join('\0', entries.Select(e => e.Item2)) + "\0");
    var bytes = new byte[20 + entries.Length * 16 + keys.Length + values.Length];
    bytes[1] = (byte)'P';
    bytes[2] = (byte)'S';
    bytes[3] = (byte)'F';
    void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    U32(8, (uint)(20 + entries.Length * 16));
    U32(12, (uint)(20 + entries.Length * 16 + keys.Length));
    U32(16, (uint)entries.Length);
    int k = 0, v = 0;
    for (int i = 0; i < entries.Length; i++)
    {
        int o = 20 + i * 16;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(o, 2), (ushort)k);
        U32(o + 4, (uint)(entries[i].Item2.Length + 1));
        U32(o + 8, (uint)(entries[i].Item2.Length + 1));
        U32(o + 12, (uint)v);
        k += entries[i].Item1.Length + 1;
        v += entries[i].Item2.Length + 1;
    }
    keys.CopyTo(bytes, 20 + entries.Length * 16);
    values.CopyTo(bytes, 20 + entries.Length * 16 + keys.Length);
    return bytes;
}
byte[] Iso(string id)
{
    var image = new byte[24 * 2048];
    image[16 * 2048] = 1;
    Encoding.ASCII.GetBytes("CD001").CopyTo(image, 16 * 2048 + 1);
    void Record(int offset, uint extent, uint size, string name)
    {
        image[offset] = (byte)(34 + name.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset + 2), extent);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset + 10), size);
        image[offset + 32] = (byte)name.Length;
        Encoding.ASCII.GetBytes(name).CopyTo(image, offset + 33);
    }
    Record(16 * 2048 + 156, 20, 2048, ".");
    Record(20 * 2048, 21, 128, "SYSTEM.CNF;1");
    Encoding.ASCII.GetBytes("BOOT2 = cdrom0:\\" + id.Insert(4, "_").Insert(8, ".") + ";1\n").CopyTo(image, 21 * 2048);
    return image;
}
byte[] ArtworkIso(byte[] png)
{
    var image = new byte[25 * 2048];
    image[16 * 2048] = 1;
    Encoding.ASCII.GetBytes("CD001").CopyTo(image, 16 * 2048 + 1);
    void Record(int offset, uint extent, uint size, string name)
    {
        image[offset] = (byte)(34 + name.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset + 2), extent);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset + 10), size);
        image[offset + 32] = (byte)name.Length;
        Encoding.ASCII.GetBytes(name).CopyTo(image, offset + 33);
    }
    Record(16 * 2048 + 156, 20, 2048, ".");
    Record(20 * 2048, 21, 2048, "PSP_GAME");
    Record(21 * 2048, 22, (uint)png.Length, "ICON0.PNG;1");
    png.CopyTo(image, 22 * 2048);
    return image;
}
try
{
    await Test("First start: six independent empty games, no emulator required globally", () => { var state = LauncherSettings.Create(); Assert(!state.SetupComplete && state.SetupStep == 0 && state.Games.Count == 6); Assert(Catalog.Games.All(g => new LaunchService(paths).Status(g, state) == GameStatus.GameFilesRequired)); return Task.CompletedTask; });
    await Test("Relative path survives collection relocation", () => { var file = Fixture("Games/a.iso", [1, 2]); var stored = paths.Store(file); Assert(stored == "./Games/a.iso"); var moved = new CollectionPaths(Path.Combine(root, "other-drive")); Assert(moved.Resolve(stored) == Path.Combine(root, "other-drive", "Games", "a.iso")); return Task.CompletedTask; });
    await Test("Traversal and absolute archive paths rejected", async () => { foreach (var bad in new[] { "../escape", "/absolute", "C:/outside", "nested/../../escape", "a:stream" }) await Reject(() => Task.FromResult(CollectionPaths.SafeCombine(root, bad))); });
    await Test("Settings round trip and resumable wizard", async () => { var state = LauncherSettings.Create(); state.SetupStep = 3; state.Emulators[EmulatorKind.PPSSPP].Prepared = true; await new SettingsStore(paths).SaveAsync(state); var read = await new SettingsStore(paths).LoadAsync(); Assert(read.SetupStep == 3 && read.Emulators[EmulatorKind.PPSSPP].Prepared); state.SetupStep = 4; await new SettingsStore(paths).SaveAsync(state); Assert(File.Exists(new SettingsStore(paths).FilePath + ".bak")); });
    await Test("Corrupt settings preserved", async () => { var separate = new CollectionPaths(Path.Combine(root, "broken")); Directory.CreateDirectory(separate.Data); var file = Path.Combine(separate.Data, "settings.json"); await File.WriteAllTextAsync(file, "broken"); await Reject(() => new SettingsStore(separate).LoadAsync()); Assert(await File.ReadAllTextAsync(file) == "broken"); });
    await Test("Manual emulator selection validates name and PE header", async () => { var exe = Fixture("emu/PPSSPPWindows64.exe", [(byte)'M', (byte)'Z', 0, 0]); Assert(EmulatorDiscovery.Validate(EmulatorKind.PPSSPP, exe).Kind == EmulatorKind.PPSSPP); await Reject(() => Task.FromResult(EmulatorDiscovery.Validate(EmulatorKind.PCSX2, exe))); var bad = Fixture("bad/pcsx2-qt.exe", [0, 1]); await Reject(() => Task.FromResult(EmulatorDiscovery.Validate(EmulatorKind.PCSX2, bad))); });
    await Test("Bounded emulator discovery", async () => { var found = await new EmulatorDiscovery().FindAsync([Path.Combine(root, "emu")]); Assert(found.Count == 1 && found[0].Kind == EmulatorKind.PPSSPP); });
    await Test("ISO metadata from SYSTEM.CNF, independent of filename", async () => { var iso = Fixture("misleading-name.iso", Iso("SCUS97399")); var meta = await new GameInspector().InspectAsync(iso); Assert(meta.GameId == "SCUS97399" && meta.Supported); });
    await Test("PARAM.SFO metadata and invalid bounds", async () => { Assert(GameInspector.ReadSfo(Sfo("UCUS98653"))["DISC_ID"] == "UCUS98653"); var bytes = Sfo("BCUS98232"); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), uint.MaxValue); await Reject(() => Task.FromResult(GameInspector.ReadSfo(bytes))); });
    await Test("Incorrect ISO rejected", async () => { var file = Fixture("wrong.iso", new byte[40000]); Assert(!(await new GameInspector().InspectAsync(file)).Supported); });
    await Test("Compressed header validation", async () => { var bad = Fixture("bad.cso", [1, 2, 3]); Assert(!(await new GameInspector().InspectAsync(bad)).Supported); var valid = Fixture("valid.cso", Encoding.ASCII.GetBytes("CISO0000")); var meta = await new GameInspector().InspectAsync(valid); Assert(meta.Supported && meta.GameId == null); });
    await Test("Only one title ready while five are absent", () => { var state = LauncherSettings.Create(); var game = state.Games["chains"]; game.Path = paths.Store(Fixture("only.iso", Iso("SCUS97399"))); game.GameId = "UCUS98653"; game.IdentityConfirmed = true; state.Emulators[EmulatorKind.PPSSPP] = new() { Executable = paths.Store(Path.Combine(root, "emu", "PPSSPPWindows64.exe")), Prepared = true }; var service = new LaunchService(paths); Assert(service.Status(Catalog.Get("chains"), state) == GameStatus.ReadyToPlay); Assert(Catalog.Games.Where(g => g.Key != "chains").All(g => service.Status(g, state) == GameStatus.GameFilesRequired)); game.Path = paths.Store(Path.Combine(root, "missing.iso")); Assert(service.Status(Catalog.Get("chains"), state) == GameStatus.GameFilesNotFound); return Task.CompletedTask; });
    await Test("Launch plans for all six titles use independent paths and arguments", () =>
    {
        foreach (var definition in Catalog.Games)
        {
            var state = LauncherSettings.Create();
            string exe = Fixture("launch-" + definition.Key + "/" + EmulatorDiscovery.Names[definition.Emulator][0], [(byte)'M', (byte)'Z', 0, 0]);
            string gamePath;
            if (definition.Emulator == EmulatorKind.RPCS3)
            {
                gamePath = Path.Combine(root, "disc-" + definition.Key);
                Fixture("disc-" + definition.Key + "/PS3_GAME/USRDIR/EBOOT.BIN", [0]);
            }
            else
                gamePath = Fixture("game with spaces " + definition.Key + ".iso", [0]);
            state.Emulators[definition.Emulator] = new()
            {
                Executable = paths.Store(exe),
                Prepared = true,
                Version = "2.6.3"
            };
            state.Games[definition.Key] = new()
            {
                Path = paths.Store(gamePath),
                GameId = definition.GameIds[0],
                IdentityConfirmed = true
            };
            var plan = new LaunchService(paths).Plan(definition, state);
            Assert(plan.Executable == exe);
            Assert(plan.Arguments.Last().Contains(definition.Emulator == EmulatorKind.RPCS3 ? "EBOOT.BIN" : "game with spaces"));
            if (definition.Emulator == EmulatorKind.PCSX2)
                Assert(plan.Arguments.Contains("--"));
        }
        return Task.CompletedTask;
    });
    await Test("Process transport preserves argument boundaries", async () => { var argsFile = Path.Combine(root, "process-args.txt"); using var process = LaunchService.Start(new(Environment.ProcessPath!, root, ["--capture-arguments", argsFile, "a b.iso", "semi;colon", "quote-value"])); await process.WaitForExitAsync(); Assert(process.ExitCode == 0 && File.ReadAllLines(argsFile).SequenceEqual(new[] { "a b.iso", "semi;colon", "quote-value" })); });
    await Test("Copy preserves originals and refuses overwrite", async () => { var file = Fixture("original.dat", [1, 3, 5]); var hash = StorageOperations.Hash(file); var target = Path.Combine(root, "copied.dat"); await StorageOperations.CopyVerifiedAsync(file, target); Assert(StorageOperations.Hash(target) == hash && StorageOperations.Hash(file) == hash); await Reject(() => StorageOperations.CopyVerifiedAsync(file, target)); });
    await Test("Cancelled copy leaves no final destination", async () => { using var cancel = new CancellationTokenSource(); cancel.Cancel(); var target = Path.Combine(root, "cancelled.dat"); await RejectCancelled(); async Task RejectCancelled() { try { await StorageOperations.CopyVerifiedAsync(Path.Combine(root, "original.dat"), target, token: cancel.Token); throw new Exception("Expected cancellation"); } catch (OperationCanceledException) { Assert(!File.Exists(target)); } } });
    await Test("Storage errors are explicit", async () => { await Reject(() => Task.Run(() => StorageOperations.RequireSpace(root, long.MaxValue / 2))); });
    var archive = Zip("textures.zip", ("UCUS98653/textures.ini", "[games]\nUCUS98653 = textures.ini"), ("UCUS98653/a.png", "legal generated texture fixture"));
    var inspector = new TexturePackInspector();
    await Test("PPSSPP texture layout detected", () => { var pack = inspector.Inspect(archive, Catalog.Get("chains"), "UCUS98653"); Assert(pack.Prefix == "UCUS98653/" && pack.FileCount == 2); return Task.CompletedTask; });
    await Test("PCSX2 serial/replacements layout detected", () => { var zip = Zip("ps2.zip", ("SCUS-97399/replacements/a.dds", "fixture")); Assert(inspector.Inspect(zip, Catalog.Get("gow1"), "SCUS97399").Profile.StartsWith("PCSX2")); return Task.CompletedTask; });
    await Test("Root textures.ini layout detected", () => { var zip = Zip("root-pack.zip", ("textures.ini", "[games]\nUCUS98653 = textures.ini"), ("a.png", "fixture")); Assert(inspector.Inspect(zip, Catalog.Get("chains"), "UCUS98653").Prefix == ""); return Task.CompletedTask; });
    await Test("Incompatible package rejected", async () => await Reject(() => Task.FromResult(inspector.Inspect(archive, Catalog.Get("ghost"), "UCUS98737"))));
    await Test("ZIP traversal, ADS, reserved names, executables and duplicate paths rejected", async () => { var i = 0; foreach (var name in new[] { "../escape.png", "C:/outside.png", "UCUS98653/a:stream.png", "UCUS98653/CON.png", "UCUS98653/setup.exe", "UCUS98653/a. /x.png" }) { var zip = Zip("unsafe" + i++ + ".zip", (name, "bad")); await Reject(() => Task.FromResult(inspector.Inspect(zip, Catalog.Get("chains"), "UCUS98653"))); } var duplicate = Zip("duplicate.zip", ("UCUS98653/a.png", "a"), ("UCUS98653/A.png", "b")); await Reject(() => Task.FromResult(inspector.Inspect(duplicate, Catalog.Get("chains"), "UCUS98653"))); });
    await Test("7Z is explicitly rejected without a safe dependency", async () => await Reject(() => Task.FromResult(inspector.Inspect(Fixture("pack.7z", [1]), Catalog.Get("chains"), "UCUS98653"))));
    await Test("Mod import, backup, enable, disable, re-enable and uninstall", async () => { var manager = new EnhancementManager(paths); var target = Path.Combine(root, "emulator-textures", "UCUS98653"); Directory.CreateDirectory(target); var existing = Path.Combine(target, "a.png"); await File.WriteAllTextAsync(existing, "original texture"); var unrelated = Path.Combine(target, "other-mod.png"); await File.WriteAllTextAsync(unrelated, "unrelated"); var mod = await manager.ImportTexturesAsync(inspector.Inspect(archive, Catalog.Get("chains"), "UCUS98653"), Catalog.Get("chains"), target, "Fixture Author"); await Reject(() => manager.EnableAsync(mod, false)); await manager.EnableAsync(mod, true); Assert(File.ReadAllText(existing) == "legal generated texture fixture"); Assert(manager.Load().Single().Enabled); await manager.DisableAsync(mod); Assert(File.ReadAllText(existing) == "original texture" && File.ReadAllText(unrelated) == "unrelated"); await manager.EnableAsync(mod, true); await manager.UninstallAsync(mod); Assert(File.ReadAllText(existing) == "original texture" && File.Exists(unrelated) && manager.Load().Count == 0); });
    await Test("Externally changed mod files preserved", async () => { var manager = new EnhancementManager(paths); var target = Path.Combine(root, "conflict"); var mod = await manager.ImportTexturesAsync(inspector.Inspect(archive, Catalog.Get("chains"), "UCUS98653"), Catalog.Get("chains"), target, "Fixture"); await manager.EnableAsync(mod, true); await File.WriteAllTextAsync(Path.Combine(target, "a.png"), "user edit"); await Reject(() => manager.DisableAsync(mod)); Assert(File.ReadAllText(Path.Combine(target, "a.png")) == "user edit"); });
    await Test("Patch identity, CRC and native RPCS3 boundary", async () => { var manager = new EnhancementManager(paths); var pnach = Fixture("SCUS-97399_12345678.pnach", Encoding.ASCII.GetBytes("// legal no-op fixture")); var game = new GameSettings { GameId = "SCUS97399", IdentityConfirmed = true }; await Reject(() => manager.ImportPatchAsync(pnach, Catalog.Get("gow1"), game, Path.Combine(root, "cheats"), EnhancementKind.Performance)); game.Crc = "87654321"; await Reject(() => manager.ImportPatchAsync(pnach, Catalog.Get("gow1"), game, Path.Combine(root, "cheats"), EnhancementKind.Performance)); game.Crc = "12345678"; var mod = await manager.ImportPatchAsync(pnach, Catalog.Get("gow1"), game, Path.Combine(root, "cheats"), EnhancementKind.Performance); await manager.EnableAsync(mod, true); await manager.DisableAsync(mod); Assert(!File.Exists(Path.Combine(root, "cheats", Path.GetFileName(pnach)))); await Reject(() => manager.ImportPatchAsync(pnach, Catalog.Get("ascension"), game, root, EnhancementKind.Performance)); });
    await Test("Backup restore keeps a previous settings backup", async () => { var state = LauncherSettings.Create(); state.SetupComplete = true; var backup = Path.Combine(root, "settings-backup.zip"); var service = new ConfigurationBackup(paths); await service.ExportAsync(backup, state); await service.RestoreAsync(backup); Assert((await new SettingsStore(paths).LoadAsync()).SetupComplete); Assert(Directory.GetFiles(Path.Combine(paths.Data, "Backups")).Length > 0); });
    await Test("Profile overlay preserves unrelated configuration", () => { var content = "[EmuCore/GS]\nupscale_multiplier = 2\nOther = keep\n[Patches]\nEnable = Native\n"; var edited = EmulatorProfiles.IniSet(content, "EmuCore/GS", "upscale_multiplier", "3"); Assert(edited.Contains("upscale_multiplier = 3") && edited.Contains("Other = keep") && edited.Contains("Enable = Native")); Assert(EmulatorProfiles.SupportsPcsx2Overlay("2.6.3.0") && !EmulatorProfiles.SupportsPcsx2Overlay("1.7.0")); return Task.CompletedTask; });
    await Test("Graphics presets and range validation", async () => { var game = new GameSettings(); GraphicsProfiles.Select(game, GraphicsPreset.UltraHD); Assert(game.ResolutionScale == 6); game.ResolutionScale = 100; await Reject(() => Task.FromResult(GraphicsProfiles.PspOverlay(game, false))); });
    await Test("Offline: core setup, library and profiles make no network requests", async () => { var state = await new SettingsStore(paths).LoadAsync(); Assert(state.Games.Count == 6); state.Games["chains"].UseLaunchProfile = true; var overlay = await new EmulatorProfiles(paths).PrepareAsync(Catalog.Get("chains"), state.Games["chains"], new(), false, false); Assert(File.Exists(overlay)); });
    await Test("Default launch preserves existing graphics and texture settings", async () => { var game = new GameSettings(); Assert(await new EmulatorProfiles(paths).PrepareAsync(Catalog.Get("chains"), game, new(), false, false) == null); Assert(!GraphicsProfiles.PspOverlay(game, false).Contains("ReplaceTextures")); });
    await Test("RPCS3 private overlay preserves game-specific fixes", async () => { var configRoot = Path.Combine(root, "rpc-data"); var original = Fixture("rpc-data/config/custom_configs/config_BCUS98232.yml", Encoding.UTF8.GetBytes("Core:\n  SPU Block Size: Safe\nVideo:\n  Renderer: Vulkan\n  Resolution Scale: 100\n  Write Color Buffers: true\nAudio:\n  Renderer: Cubeb\n")); var hash = StorageOperations.Hash(original); var game = new GameSettings { GameId = "BCUS98232", UseLaunchProfile = true, ResolutionScale = 3 }; var overlay = await new EmulatorProfiles(paths).PrepareAsync(Catalog.Get("ascension"), game, new() { DataDirectory = paths.Store(configRoot) }, false, false); Assert(File.ReadAllText(overlay!).Contains("Resolution Scale: 300")); Assert(File.ReadAllText(overlay!).Contains("Write Color Buffers: true")); Assert(StorageOperations.Hash(original) == hash); });
    await Test("Unsupported YAML and duplicated scalar rejected", async () => { await Reject(() => Task.FromResult(EmulatorProfiles.YamlScalar("Video: {}", "Video", "Resolution Scale", "300"))); await Reject(() => Task.FromResult(EmulatorProfiles.YamlScalar("Video:\n  Resolution Scale: 100\n  Resolution Scale: 200", "Video", "Resolution Scale", "300"))); });
    await Test("Directory copy includes bin and cache contents without modifying sources", async () => { var source = Path.Combine(root, "full-tree"); Fixture("full-tree/bin/runtime.dat", [1, 2]); Fixture("full-tree/cache/user.dat", [3, 4]); var target = Path.Combine(root, "full-tree-copy"); await StorageOperations.CopyVerifiedAsync(source, target); Assert(File.Exists(Path.Combine(target, "bin", "runtime.dat")) && File.Exists(Path.Combine(target, "cache", "user.dat"))); });
    await Test("PSP patches require revision, emulator range and file hash", async () => { var manager = new EnhancementManager(paths); var game = new GameSettings { GameId = "UCUS98653", Revision = "1.00", IdentityConfirmed = true }; var file = Fixture("psp-patch.ini", Encoding.ASCII.GetBytes("_S UCUS-98653\n_G Generated no-op fixture\n")); await Reject(() => manager.ImportPatchAsync(file, Catalog.Get("chains"), game, root, EnhancementKind.Performance, emulatorVersion: "v1.20.4")); var metadata = new PatchDescriptor("UCUS98653", "1.00", "1.20.4", "1.20.4", StorageOperations.Hash(file), "https://example.org/legal-test-fixture", "Generated fixture"); await File.WriteAllTextAsync(file + ".klc.json", System.Text.Json.JsonSerializer.Serialize(metadata, SettingsStore.Json)); await Reject(() => manager.ImportPatchAsync(file, Catalog.Get("chains"), game, root, EnhancementKind.Performance, emulatorVersion: "v1.19.0")); var mod = await manager.ImportPatchAsync(file, Catalog.Get("chains"), game, root, EnhancementKind.Performance, emulatorVersion: "v1.20.4"); EnhancementManager.RequireCompatible(mod, Catalog.Get("chains"), game, new() { Version = "v1.20.4" }); game.Revision = "2.00"; await Reject(() => Task.Run(() => EnhancementManager.RequireCompatible(mod, Catalog.Get("chains"), game, new() { Version = "v1.20.4" }))); });
    await Test("Archive symbolic links rejected", async () => { var zip = Path.Combine(root, "symlink.zip"); using (var archiveFile = ZipFile.Open(zip, ZipArchiveMode.Create)) { var entry = archiveFile.CreateEntry("UCUS98653/link.png"); entry.ExternalAttributes = unchecked((int)0xA1FF0000); } await Reject(() => Task.FromResult(inspector.Inspect(zip, Catalog.Get("chains"), "UCUS98653"))); });
    await Test("Highly compressed expansion rejected", async () => { var zip = Path.Combine(root, "expansion.zip"); using (var archiveFile = ZipFile.Open(zip, ZipArchiveMode.Create)) { var entry = archiveFile.CreateEntry("UCUS98653/a.png", CompressionLevel.SmallestSize); using var stream = entry.Open(); stream.Write(new byte[2_000_000]); } await Reject(() => Task.FromResult(inspector.Inspect(zip, Catalog.Get("chains"), "UCUS98653"))); });
    await Test("Overlapping active mods are rejected", async () => { var local = new CollectionPaths(Path.Combine(root, "overlap")); var manager = new EnhancementManager(local); var target = Path.Combine(root, "overlap-target"); var pack = inspector.Inspect(archive, Catalog.Get("chains"), "UCUS98653"); var first = await manager.ImportTexturesAsync(pack, Catalog.Get("chains"), target, "Fixture"); var second = await manager.ImportTexturesAsync(pack, Catalog.Get("chains"), target, "Fixture"); await manager.EnableAsync(first, true); await Reject(() => manager.EnableAsync(second, true)); Assert(first.Enabled && !second.Enabled); await manager.UninstallAsync(first); await manager.UninstallAsync(second); });
    await Test("Invalid graphics settings cannot replace valid persisted settings", async () => { var state = await new SettingsStore(paths).LoadAsync(); var original = StorageOperations.Hash(new SettingsStore(paths).FilePath); state.Games["chains"].ResolutionScale = 999; await Reject(() => new SettingsStore(paths).SaveAsync(state)); Assert(StorageOperations.Hash(new SettingsStore(paths).FilePath) == original); });
    var pixel = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    await Test("Artwork extracted from generated PSP ISO without modifying it", async () => { var file = Fixture("legal-artwork.iso", ArtworkIso(pixel)); var hash = StorageOperations.Hash(file); var bytes = await new GameInspector().ReadArtworkAsync(file); Assert(bytes != null && bytes.SequenceEqual(pixel)); var stored = await new ArtworkService(paths).SaveAsync(Catalog.Get("chains"), bytes!); Assert(stored.StartsWith("./Data/Artwork/") && File.Exists(paths.Resolve(stored))); Assert(StorageOperations.Hash(file) == hash); });
    await Test("PS3 artwork uses game folder and supports ICON0 fallback", async () => { var folder = Path.Combine(root, "art-disc"); Fixture("art-disc/PS3_GAME/ICON0.PNG", pixel); Assert((await new GameInspector().ReadArtworkAsync(folder))!.SequenceEqual(pixel)); });
    await Test("PS2 image without standard artwork has an honest empty result", async () => { Assert(await new GameInspector().ReadArtworkAsync(Path.Combine(root, "misleading-name.iso")) == null); });
    await Test("Artwork rejects unknown files and oversized pixel dimensions", async () => { await Reject(() => new ArtworkService(paths).SaveAsync(Catalog.Get("chains"), new byte[24])); var huge = (byte[])pixel.Clone(); BinaryPrimitives.WriteUInt32BigEndian(huge.AsSpan(16), 100000); await Reject(() => new ArtworkService(paths).SaveAsync(Catalog.Get("chains"), huge)); });
    await Test("Chronological and requested release order contain all six titles", async () => { Assert(Catalog.Ordered(GameSortOrder.Chronological).Select(g => g.Key).SequenceEqual(new[] { "ascension", "chains", "gow1", "ghost", "gow2", "gow3" })); Assert(Catalog.Ordered(GameSortOrder.Release).Select(g => g.Key).SequenceEqual(new[] { "gow1", "gow2", "chains", "ghost", "gow3", "ascension" })); var state = LauncherSettings.Create(); state.SortOrder = GameSortOrder.Chronological; var separate = new CollectionPaths(Path.Combine(root, "sort-settings")); await new SettingsStore(separate).SaveAsync(state); Assert((await new SettingsStore(separate).LoadAsync()).SortOrder == GameSortOrder.Chronological); });
    await Test("Portable emulator preparation copies a full data tree and leaves originals intact", async () => { var exe = Fixture("portable-source/pcsx2-qt.exe", [(byte)'M', (byte)'Z', 0, 0]); var ini = Fixture("portable-data/inis/PCSX2.ini", Encoding.UTF8.GetBytes("[EmuCore/GS]\nupscale_multiplier = 3\n")); Fixture("portable-data/textures/SCUS-97399/replacements/a.png", pixel); var target = Path.Combine(root, "portable-ready"); var prepared = await new PortablePreparation(paths).PrepareAsync(EmulatorKind.PCSX2, new() { Executable = paths.Store(exe), DataDirectory = paths.Store(Path.GetDirectoryName(Path.GetDirectoryName(ini))!) }, target); Assert(File.Exists(Path.Combine(target, "portable.ini")) && File.Exists(Path.Combine(target, "inis", "PCSX2.ini"))); Assert(File.ReadAllText(ini).Contains("upscale_multiplier = 3") && File.Exists(exe)); Assert(prepared.DataDirectory == target); });
    await Test("Portable data conflicts leave no published destination or staging copy", async () => { var exe = Fixture("conflict-bin/pcsx2-qt.exe", [(byte)'M', (byte)'Z', 0, 0]); Fixture("conflict-bin/inis/PCSX2.ini", Encoding.UTF8.GetBytes("binary-side config")); var ini = Fixture("conflict-data/inis/PCSX2.ini", Encoding.UTF8.GetBytes("user-side config")); var target = Path.Combine(root, "conflict-portable"); await Reject(() => new PortablePreparation(paths).PrepareAsync(EmulatorKind.PCSX2, new() { Executable = paths.Store(exe), DataDirectory = paths.Store(Path.GetDirectoryName(Path.GetDirectoryName(ini))!) }, target)); Assert(!Directory.Exists(target) && Directory.GetDirectories(root, "conflict-portable.prepare-*").Length == 0); Assert(File.ReadAllText(ini) == "user-side config"); });
    await Test("Catalog rejects unrelated or unreviewed regional identifiers", () => { Assert(Catalog.Find("SCPS15090") == null && Catalog.Find("UCJS10086") == null); Assert(Catalog.Find("UCAS40323")?.Key == "ghost" && Catalog.Find("ULJM05348")?.Key == "chains"); return Task.CompletedTask; });
    await Test("PPSSPP profile launches disable settings writes and restore automatic boot", async () =>
    {
        var state = LauncherSettings.Create();
        var exe = Fixture("psp-profile-bin/PPSSPPWindows64.exe", [(byte)'M', (byte)'Z', 0, 0]);
        var image = Fixture("psp-profile-game.iso", [0]);
        state.Emulators[EmulatorKind.PPSSPP] = new()
        {
            Executable = paths.Store(exe),
            Version = "v1.20.4",
            Prepared = true
        };
        state.Games["chains"] = new()
        {
            Path = paths.Store(image),
            GameId = "UCUS98653",
            IdentityConfirmed = true,
            UseLaunchProfile = true,
            ResolutionScale = 2
        };
        var profile = await new EmulatorProfiles(paths).PrepareAsync(Catalog.Get("chains"), state.Games["chains"], state.Emulators[EmulatorKind.PPSSPP], true, true);
        var text = File.ReadAllText(profile!);
        Assert(text.Contains("AutoRun = True") && text.Contains("InternalResolution = 2") && text.Contains("EnableCheats = True"));
        Assert(text.Split("[General]").Length == 2);
        var plan = new LaunchService(paths).Plan(Catalog.Get("chains"), state, profile);
        Assert(plan.Arguments.Contains("-s") && plan.Arguments.Contains("--appendconfig=" + profile));
        Assert(!new LaunchService(paths).Plan(Catalog.Get("chains"), state).Arguments.Contains("-s"));
    });
    await Test("RPCS3 refuses unsupported renderers before writing a profile", async () =>
    {
        await Reject(() => new EmulatorProfiles(paths).PrepareAsync(Catalog.Get("gow3"), new() { UseLaunchProfile = true, Renderer = "Direct3D 11" }, new(), false, false));
    });
    await Test("Volume-relative games outside the collection follow the current drive", async () =>
    {
        var stored = "@volume/OwnedGames/GodOfWarIII/PS3_GAME/USRDIR/EBOOT.BIN";
        var resolved = paths.Resolve(stored);
        Assert(resolved == Path.Combine(Path.GetPathRoot(paths.Root)!, "OwnedGames", "GodOfWarIII", "PS3_GAME", "USRDIR", "EBOOT.BIN"));
        await Reject(() => Task.FromResult(paths.Resolve("@volume/../outside")));
        await Reject(() => Task.FromResult(paths.Resolve("@volume/C:/outside")));
        if (OperatingSystem.IsWindows())
        {
            var first = new CollectionPaths(@"X:\Collection");
            var moved = new CollectionPaths(@"Y:\Collection");
            Assert(first.Resolve(stored).StartsWith(@"X:\OwnedGames\") && moved.Resolve(stored).StartsWith(@"Y:\OwnedGames\"));
        }
    });
    if (args.Contains("--inventory"))
    {
        foreach (var path in args.SkipWhile(a => a != "--inventory").Skip(1))
        {
            var metadata = await new GameInspector().InspectAsync(path);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(metadata));
        }
    }
    if (args.Contains("--extract-artwork"))
    {
        var parameters = args.SkipWhile(a => a != "--extract-artwork").Skip(1).ToArray();
        if (parameters.Length != 3)
            throw new Exception("Expected game key, source game and private collection folder");
        var bytes = await new GameInspector().ReadArtworkAsync(parameters[1]);
        if (bytes == null)
            Console.WriteLine("No artwork found in game files.");
        else
            Console.WriteLine(await new ArtworkService(new CollectionPaths(parameters[2])).SaveAsync(Catalog.Get(parameters[0]), bytes));
    }
}
finally
{
    Console.WriteLine($"RESULT {results.Count(r => r.passed)}/{results.Count} passed");
    foreach (var failed in results.Where(r => !r.passed))
        Console.WriteLine(failed.error);
    Directory.Delete(root, true);
}
return results.All(r => r.passed) ? 0 : 1;
