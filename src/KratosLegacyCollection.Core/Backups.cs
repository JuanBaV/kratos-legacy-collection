using System.IO.Compression;
using System.Text.Json;

namespace KratosLegacyCollection.Core;

public sealed class ConfigurationBackup(CollectionPaths paths)
{
    public async Task ExportAsync(string destination, LauncherSettings state, CancellationToken token = default) {
        if (File.Exists(destination)) throw new UserError("Choose a new backup filename. Existing backups are preserved.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
        using var zip = new ZipArchive(output, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("settings.json");
        await using var stream = entry.Open(); await JsonSerializer.SerializeAsync(stream, state, SettingsStore.Json, token);
    }
    public async Task<LauncherSettings> ReadAsync(string source, CancellationToken token = default) {
        using var zip = ZipFile.OpenRead(source);
        if (zip.Entries.Count != 1 || zip.Entries[0].FullName != "settings.json" || zip.Entries[0].Length > 5_000_000) throw new UserError("This is not a launcher configuration backup.");
        TexturePackInspector.ValidateEntry(zip.Entries[0]);
        await using var stream = zip.Entries[0].Open();
        var state = await JsonSerializer.DeserializeAsync<LauncherSettings>(stream, SettingsStore.Json, token) ?? throw new UserError("The backup is empty.");
        if (state.SchemaVersion != 1 || state.Games == null || state.Emulators == null) throw new UserError("The backup schema is unsupported.");
        foreach (var game in Catalog.Games) state.Games.TryAdd(game.Key, new());
        foreach (var kind in Enum.GetValues<EmulatorKind>()) state.Emulators.TryAdd(kind, new());
        return state;
    }
    public async Task RestoreAsync(string source, CancellationToken token = default) {
        var restored = await ReadAsync(source, token);
        var store = new SettingsStore(paths);
        if (File.Exists(store.FilePath)) {
            var backup = Path.Combine(paths.Data, "Backups", $"before-restore-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip");
            await ExportAsync(backup, await store.LoadAsync(token), token);
        }
        await store.SaveAsync(restored, token);
    }
}

public sealed class PortablePreparation(CollectionPaths paths)
{
    public async Task<EmulatorCandidate> PrepareAsync(EmulatorKind kind, EmulatorSettings emulator, string destination, IProgress<string>? progress = null, CancellationToken token = default) {
        if (kind == EmulatorKind.RPCS3) throw new UserError("RPCS3 uses your existing installation. Its firmware, cache and game data require separate space planning.");
        if (emulator.Executable == null) throw new UserError("Select an emulator first.");
        var exe = paths.Resolve(emulator.Executable); EmulatorDiscovery.Validate(kind, exe);
        var binaryRoot = Path.GetDirectoryName(exe)!;
        if (Directory.Exists(destination) || File.Exists(destination)) throw new UserError("Choose a new destination folder.");
        var staging = destination + ".prepare-" + Guid.NewGuid().ToString("N");
        try {
            await StorageOperations.CopyVerifiedAsync(binaryRoot, staging, progress, token);
            if (emulator.DataDirectory != null) {
                var data = paths.Resolve(emulator.DataDirectory);
                if (Directory.Exists(data) && !data.Equals(binaryRoot, StringComparison.OrdinalIgnoreCase)) {
                    var target = kind == EmulatorKind.PPSSPP ? Path.Combine(staging, "memstick") : staging;
                    var files = StorageOperations.CopyFiles(data, token);
                    StorageOperations.RequireSpace(staging, files.Sum(f => new FileInfo(f).Length));
                    foreach (var file in files) {
                        token.ThrowIfCancellationRequested();
                        var copy = CollectionPaths.SafeCombine(target, Path.GetRelativePath(data, file));
                        if (File.Exists(copy)) {
                            if (StorageOperations.Hash(copy) == StorageOperations.Hash(file)) continue;
                            throw new UserError("The installation and data folders contain conflicting files. Choose a clean official portable emulator folder and import your data there.");
                        }
                        await StorageOperations.CopyVerifiedAsync(file, copy, progress, token);
                    }
                }
            }
            if (kind == EmulatorKind.PCSX2) await File.WriteAllTextAsync(Path.Combine(staging, "portable.ini"), "", token);
            else {
                var redirect = Path.Combine(staging, "installed.txt");
                if (File.Exists(redirect)) File.Delete(redirect);
            }
            Directory.Move(staging, destination);
            return new(kind, Path.Combine(destination, Path.GetFileName(exe)), emulator.Version, kind == EmulatorKind.PPSSPP ? Path.Combine(destination, "memstick") : destination);
        } finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
}
