using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KratosLegacyCollection.Core;

public sealed record PackInspection(string Archive, string GameId, string Prefix, string Profile, long ExpandedBytes, int FileCount);

public sealed partial class TexturePackInspector
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase) { ".png", ".dds", ".jpg", ".jpeg", ".ktx", ".ktx2", ".basis", ".ini", ".txt", ".md" };
    public PackInspection Inspect(string archive, GameDefinition game, string gameId) {
        if (!Path.GetExtension(archive).Equals(".zip", StringComparison.OrdinalIgnoreCase)) throw new UserError("This build supports ZIP texture packs. Extract 7Z with a trusted tool, then create a ZIP of the textures. Never run bundled scripts.");
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count is 0 or > 100_000) throw new UserError("The archive has an unsupported number of entries.");
        var files = new List<string>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long expanded = 0;
        foreach (var entry in zip.Entries) {
            ValidateEntry(entry);
            if (entry.FullName.EndsWith('/')) continue;
            var name = entry.FullName.Replace('\\', '/');
            if (!names.Add(name)) throw new UserError("The archive contains duplicate filenames.");
            if (!Allowed.Contains(Path.GetExtension(name))) throw new UserError($"Unsupported texture-pack content: {Path.GetFileName(name)}. Only textures and descriptive files are accepted.");
            expanded = checked(expanded + entry.Length); files.Add(name);
        }
        if (expanded > 32L * 1024 * 1024 * 1024) throw new UserError("The expanded package exceeds the 32 GB safety limit.");
        var id = Catalog.NormalizeId(gameId);
        if (!game.GameIds.Contains(id)) throw new UserError("The game identity has not been verified.");
        var roots = new List<(string prefix, string profile)>();
        foreach (var file in files) {
            var segments = file.Split('/');
            for (var i = 0; i < segments.Length - 1; i++) {
                if (Catalog.NormalizeId(segments[i]) != id) continue;
                var prefix = string.Join('/', segments.Take(i + 1)) + "/";
                if (game.Emulator == EmulatorKind.PCSX2 && files.Any(f => f.StartsWith(prefix + "replacements/", StringComparison.OrdinalIgnoreCase))) roots.Add((prefix, "PCSX2 serial/replacements"));
                if (game.Emulator == EmulatorKind.PPSSPP && files.Contains(prefix + "textures.ini", StringComparer.OrdinalIgnoreCase)) roots.Add((prefix, "PPSSPP Game ID/textures.ini"));
            }
        }
        if (game.Emulator == EmulatorKind.PPSSPP && files.Contains("textures.ini", StringComparer.OrdinalIgnoreCase)) {
            using var reader = new StreamReader(zip.Entries.Single(e => e.FullName.Equals("textures.ini", StringComparison.OrdinalIgnoreCase)).Open());
            var text = reader.ReadToEnd();
            if (text.Length > 2_000_000) throw new UserError("textures.ini exceeds the metadata limit.");
            if (GameIdRegex().Matches(text).Any(m => Catalog.NormalizeId(m.Value) == id)) roots.Add(("", "PPSSPP root textures.ini with identity"));
        }
        var choices = roots.Distinct().ToArray();
        if (choices.Length != 1) throw new UserError("A unique compatible layout was not found. Use a single Game ID folder with replacements (PCSX2), or Game ID/textures.ini (PPSSPP). RPCS3 texture injection is not supported.");
        if (!files.Any(f => f.StartsWith(choices[0].prefix, StringComparison.OrdinalIgnoreCase) && Path.GetExtension(f).ToLowerInvariant() is ".png" or ".dds" or ".ktx2" or ".jpg")) throw new UserError("No replacement textures were found.");
        return new(archive, id, choices[0].prefix, choices[0].profile, expanded, files.Count);
    }
    public static void ValidateEntry(ZipArchiveEntry entry) {
        var name = entry.FullName.Replace('\\', '/');
        if (name.Length > 240 || name.StartsWith('/') || name.Contains(':') || name.Split('/').Any(s => s is "." or ".." || s.EndsWith(' ') || s.EndsWith('.') || s.Any(c => c < 32 || "<>\"|?*".Contains(c)) || ReservedRegex().IsMatch(s)))
            throw new UserError("The archive contains an unsafe Windows path.");
        if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0) throw new UserError("Archive links are not supported.");
        if (entry.Length > 8L * 1024 * 1024 * 1024 || entry.Length > 1024 * 1024 && entry.Length / Math.Max(1, entry.CompressedLength) > 1000) throw new UserError("The archive exceeds safe extraction limits.");
    }
    [GeneratedRegex(@"[A-Z]{4}-?\d{5}", RegexOptions.IgnoreCase)] private static partial Regex GameIdRegex();
    [GeneratedRegex(@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", RegexOptions.IgnoreCase)] private static partial Regex ReservedRegex();
}

public sealed class EnhancementManager(CollectionPaths paths)
{
    private string Store => Path.Combine(paths.Data, "Enhancements");
    private string RecordPath(string id) => CollectionPaths.SafeCombine(Store, id + "/manifest.json");
    public IReadOnlyList<InstalledEnhancement> Load() {
        if (!Directory.Exists(Store)) return [];
        return Directory.GetFiles(Store, "manifest.json", SearchOption.AllDirectories).Select(f => JsonSerializer.Deserialize<InstalledEnhancement>(File.ReadAllText(f), SettingsStore.Json) ?? throw new UserError("An enhancement record is damaged.")).ToArray();
    }
    private async Task SaveAsync(InstalledEnhancement mod) {
        var file = RecordPath(mod.Id); Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temp = file + ".tmp"; await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(mod, SettingsStore.Json)); File.Move(temp, file, true);
    }
    public async Task<InstalledEnhancement> ImportTexturesAsync(PackInspection pack, GameDefinition game, string target, string author, CancellationToken token = default) {
        var checkedPack = new TexturePackInspector().Inspect(pack.Archive, game, pack.GameId);
        if (checkedPack != pack) throw new UserError("The package changed after inspection. Inspect it again.");
        StorageOperations.RequireSpace(paths.Data, pack.ExpandedBytes);
        var mod = new InstalledEnhancement { GameKey = game.Key, GameId = pack.GameId, Name = Path.GetFileNameWithoutExtension(pack.Archive), Author = string.IsNullOrWhiteSpace(author) ? "User supplied" : author, TargetDirectory = paths.Store(target), Kind = EnhancementKind.Textures };
        var payload = CollectionPaths.SafeCombine(Store, mod.Id + "/payload");
        mod.PayloadDirectory = paths.Store(payload);
        try {
            using var zip = ZipFile.OpenRead(pack.Archive);
            foreach (var entry in zip.Entries) {
                token.ThrowIfCancellationRequested(); TexturePackInspector.ValidateEntry(entry);
                var name = entry.FullName.Replace('\\', '/');
                if (name.EndsWith('/') || !name.StartsWith(pack.Prefix, StringComparison.OrdinalIgnoreCase)) continue;
                var relative = name[pack.Prefix.Length..]; var destination = CollectionPaths.SafeCombine(payload, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var input = entry.Open(); await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true);
                await input.CopyToAsync(output, token);
            }
            await SaveAsync(mod); return mod;
        } catch { if (Directory.Exists(Path.GetDirectoryName(payload)!)) Directory.Delete(Path.GetDirectoryName(payload)!, true); throw; }
    }
    public async Task<InstalledEnhancement> ImportPatchAsync(string file, GameDefinition definition, GameSettings game, string target, EnhancementKind kind, CancellationToken token = default) {
        if (!game.IdentityConfirmed || game.GameId == null) throw new UserError("Verify the game identity before importing a patch.");
        if (new FileInfo(file).Length > 2_000_000) throw new UserError("The patch exceeds the 2 MB safety limit.");
        var text = await File.ReadAllTextAsync(file, token);
        string targetName;
        if (definition.Emulator == EmulatorKind.PCSX2) {
            if (game.Crc == null || !Regex.IsMatch(game.Crc, "^[0-9A-Fa-f]{8}$")) throw new UserError("Enter the exact ELF CRC reported by PCSX2 first. ISO file hashes are not ELF CRCs.");
            if (!Path.GetExtension(file).Equals(".pnach", StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(file).Contains(game.Crc, StringComparison.OrdinalIgnoreCase)) throw new UserError("The PNACH filename must contain this game's exact ELF CRC.");
            targetName = Path.GetFileName(file);
        } else if (definition.Emulator == EmulatorKind.PPSSPP) {
            if (!Path.GetExtension(file).Equals(".ini", StringComparison.OrdinalIgnoreCase) || !Regex.IsMatch(text, @"(?m)^_S\s+" + Regex.Escape(game.GameId.Insert(4, "-")) + @"\s*$", RegexOptions.IgnoreCase)) throw new UserError("The cheat file's _S Game ID must exactly match this game.");
            targetName = game.GameId + ".ini";
        } else throw new UserError("Use RPCS3's native Manage > Game Patches dialog. It validates PPU hashes and game versions. No unverified RPCS3 patch is applied by the launcher.");
        if (string.IsNullOrWhiteSpace(game.Revision) && definition.Emulator == EmulatorKind.PPSSPP) throw new UserError("Confirm the game revision before importing a PSP patch.");
        var mod = new InstalledEnhancement { GameKey = definition.Key, GameId = game.GameId, Name = Path.GetFileNameWithoutExtension(file), Kind = kind, Version = game.Revision ?? game.Crc!, TargetDirectory = paths.Store(target) };
        var payload = CollectionPaths.SafeCombine(Store, mod.Id + "/payload"); Directory.CreateDirectory(payload);
        mod.PayloadDirectory = paths.Store(payload);
        await StorageOperations.CopyVerifiedAsync(file, CollectionPaths.SafeCombine(payload, targetName), token: token);
        await SaveAsync(mod); return mod;
    }
    public async Task EnableAsync(InstalledEnhancement mod, bool allowOverwrite, CancellationToken token = default) {
        if (mod.Enabled) throw new UserError("This enhancement is already enabled.");
        var target = paths.Resolve(mod.TargetDirectory); var payload = paths.Resolve(mod.PayloadDirectory);
        var sourceFiles = StorageOperations.EnumerateSafe(payload, 64, token).ToArray();
        if (sourceFiles.Length == 0) throw new UserError("The stored payload is missing.");
        var active = Load().Where(m => m.Enabled && m.Id != mod.Id).ToArray();
        var owned = new List<OwnedFile>(); long size = 0;
        foreach (var source in sourceFiles) {
            var relative = Path.GetRelativePath(payload, source); var file = CollectionPaths.SafeCombine(target, relative);
            if (active.Any(m => m.Files.Any(f => CollectionPaths.SafeCombine(paths.Resolve(m.TargetDirectory), f.RelativePath).Equals(file, StringComparison.OrdinalIgnoreCase)))) throw new UserError("Another enabled enhancement owns one of these files. Disable it first.");
            if (File.Exists(file) && !allowOverwrite) throw new UserError("Existing files require consent and a backup. Enable again and approve the overwrite.");
            size += new FileInfo(source).Length;
            owned.Add(new() { RelativePath = relative, Hash = StorageOperations.Hash(source) });
        }
        StorageOperations.RequireSpace(target, size); StorageOperations.RequireSpace(paths.Data, size);
        foreach (var file in owned) {
            var destination = CollectionPaths.SafeCombine(target, file.RelativePath);
            if (!File.Exists(destination)) continue;
            var backup = CollectionPaths.SafeCombine(Store, mod.Id + "/backup/" + file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            File.Copy(destination, backup, true); file.BackupPath = paths.Store(backup);
        }
        mod.Files = owned; mod.Enabled = true;
        // Persist ownership before the first external write, so an interrupted install can be restored.
        await SaveAsync(mod);
        try {
            foreach (var file in owned) {
                token.ThrowIfCancellationRequested();
                var destination = CollectionPaths.SafeCombine(target, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var temp = destination + ".klc-" + mod.Id;
                File.Copy(CollectionPaths.SafeCombine(payload, file.RelativePath), temp, true);
                File.Move(temp, destination, true);
            }
        } catch { await DisableAsync(mod); throw; }
    }
    public async Task DisableAsync(InstalledEnhancement mod) {
        if (!mod.Enabled) return;
        var target = paths.Resolve(mod.TargetDirectory);
        foreach (var file in mod.Files) {
            var destination = CollectionPaths.SafeCombine(target, file.RelativePath);
            if (File.Exists(destination) && StorageOperations.Hash(destination) != file.Hash) {
                if (file.BackupPath != null && File.Exists(paths.Resolve(file.BackupPath)) && StorageOperations.Hash(destination) == StorageOperations.Hash(paths.Resolve(file.BackupPath))) continue;
                throw new UserError("An installed file changed outside the launcher. It was preserved. Resolve the conflict before restoring this enhancement.");
            }
        }
        foreach (var file in mod.Files) {
            var destination = CollectionPaths.SafeCombine(target, file.RelativePath);
            if (file.BackupPath != null) {
                var backup = paths.Resolve(file.BackupPath);
                if (!File.Exists(backup)) throw new UserError("A backup is missing. The enhancement remains registered for recovery.");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(backup, destination, true);
            } else if (File.Exists(destination)) File.Delete(destination);
        }
        mod.Enabled = false; await SaveAsync(mod);
    }
    public async Task UninstallAsync(InstalledEnhancement mod) {
        await DisableAsync(mod);
        var folder = CollectionPaths.SafeCombine(Store, mod.Id); Directory.Delete(folder, true);
    }
}
