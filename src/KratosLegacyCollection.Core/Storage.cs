using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KratosLegacyCollection.Core;

public sealed class CollectionPaths
{
    public string Root { get; }
    public string Data => System.IO.Path.Combine(Root, "Data");
    public CollectionPaths(string root) { Root = System.IO.Path.GetFullPath(root); }
    public string Store(string absolute) {
        var full = System.IO.Path.GetFullPath(absolute);
        return IsWithin(Root, full) ? "./" + System.IO.Path.GetRelativePath(Root, full).Replace('\\', '/') : full;
    }
    public string Resolve(string stored) {
        if (stored.StartsWith("./", StringComparison.Ordinal)) return SafeCombine(Root, stored[2..]);
        if (!System.IO.Path.IsPathFullyQualified(stored)) throw new UserError("The stored path is invalid. Select the file again.");
        return System.IO.Path.GetFullPath(stored);
    }
    public static bool IsWithin(string root, string path) {
        var prefix = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(root)) + System.IO.Path.DirectorySeparatorChar;
        return System.IO.Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
    public static string SafeCombine(string root, string relative) {
        if (string.IsNullOrWhiteSpace(relative) || System.IO.Path.IsPathRooted(relative) || relative.Contains(':'))
            throw new UserError("The package contains an unsafe path.");
        var result = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar).Replace('\\', System.IO.Path.DirectorySeparatorChar)));
        if (!IsWithin(root, result)) throw new UserError("The package attempts to write outside its destination.");
        AssertNoLinks(root, result);
        return result;
    }
    public static void AssertNoLinks(string root, string path) {
        var current = System.IO.Path.GetFullPath(path);
        var boundary = System.IO.Path.GetFullPath(root);
        while (current.Length >= boundary.Length) {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new UserError("Symbolic links and junctions are not allowed in managed destinations.");
            if (string.Equals(current, boundary, StringComparison.OrdinalIgnoreCase)) break;
            current = System.IO.Path.GetDirectoryName(current) ?? "";
        }
    }
}

public sealed class SettingsStore(CollectionPaths paths)
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public string FilePath => Path.Combine(paths.Data, "settings.json");
    public async Task<LauncherSettings> LoadAsync(CancellationToken token = default) {
        if (!File.Exists(FilePath)) return LauncherSettings.Create();
        try {
            await using var stream = File.OpenRead(FilePath);
            var state = await JsonSerializer.DeserializeAsync<LauncherSettings>(stream, Json, token) ?? throw new JsonException();
            if (state.SchemaVersion != 1) throw new UserError("This configuration uses an unsupported version. Update the launcher.");
            foreach (var kind in Enum.GetValues<EmulatorKind>()) state.Emulators.TryAdd(kind, new());
            foreach (var game in Catalog.Games) state.Games.TryAdd(game.Key, new());
            state.SetupStep = Math.Clamp(state.SetupStep, 0, 4);
            Validate(state);
            return state;
        } catch (JsonException) { throw new UserError("Settings could not be read. The original file was preserved. Restore settings.json.bak from Data or select another data folder."); }
    }
    public async Task SaveAsync(LauncherSettings settings, CancellationToken token = default) {
        Validate(settings);
        Directory.CreateDirectory(paths.Data);
        CollectionPaths.AssertNoLinks(paths.Root, paths.Data);
        var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true)) {
                await JsonSerializer.SerializeAsync(stream, settings, Json, token);
                await stream.FlushAsync(token);
                stream.Flush(true);
            }
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
            else File.Move(temp, FilePath);
        } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private void Validate(LauncherSettings settings) {
        if (settings.Games == null || settings.Emulators == null || settings.Games.Values.Any(g => g == null) || settings.Emulators.Values.Any(e => e == null)) throw new UserError("The configuration contains an invalid game or emulator entry.");
        foreach (var game in settings.Games.Values) {
            if (game.ResolutionScale is < 1 or > 10) throw new UserError("Resolution scale must be between 1 and 10.");
            if (!string.IsNullOrWhiteSpace(game.Path)) paths.Resolve(game.Path);
        }
        foreach (var emulator in settings.Emulators.Values) {
            if (!string.IsNullOrWhiteSpace(emulator.Executable)) paths.Resolve(emulator.Executable);
            if (!string.IsNullOrWhiteSpace(emulator.DataDirectory)) paths.Resolve(emulator.DataDirectory);
        }
    }
}

public sealed class DiagnosticLog(CollectionPaths paths)
{
    private readonly object gate = new();
    public void Write(string operation, Exception? error = null) {
        try {
            lock (gate) {
                var folder = Path.Combine(paths.Data, "Logs");
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, "launcher.log");
                if (File.Exists(file) && new FileInfo(file).Length > 2_000_000) File.Move(file, file + ".old", true);
                File.AppendAllText(file, $"{DateTimeOffset.UtcNow:O} {operation} {error?.GetType().Name} {error?.Message}{Environment.NewLine}");
            }
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

public static class StorageOperations
{
    public static void RequireSpace(string destination, long bytes) {
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(destination))!);
        if (!drive.IsReady) throw new UserError("The destination drive is disconnected.");
        if (bytes < 0 || drive.AvailableFreeSpace < bytes + 32L * 1024 * 1024)
            throw new UserError("There is not enough free space. Choose another destination. No existing files were removed.");
    }
    public static string Hash(string file) {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
    public static IEnumerable<string> EnumerateSafe(string root, int maxDepth = 12, CancellationToken token = default) {
        var pending = new Stack<(string path, int depth)>(); pending.Push((root, 0));
        while (pending.Count > 0) {
            token.ThrowIfCancellationRequested();
            var (folder, depth) = pending.Pop();
            string[] entries;
            try { entries = Directory.GetFileSystemEntries(folder); } catch (IOException) { continue; } catch (UnauthorizedAccessException) { continue; }
            foreach (var entry in entries) {
                token.ThrowIfCancellationRequested();
                FileAttributes attr;
                try { attr = File.GetAttributes(entry); } catch (IOException) { continue; } catch (UnauthorizedAccessException) { continue; }
                if ((attr & FileAttributes.ReparsePoint) != 0) continue;
                if ((attr & FileAttributes.Directory) == 0) yield return entry;
                else if (depth < maxDepth && !Excluded.Contains(Path.GetFileName(entry))) pending.Push((entry, depth + 1));
            }
        }
    }
    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase) {
        "Windows", "WindowsApps", "$Recycle.Bin", "System Volume Information", ".git", "node_modules", "obj", "bin", "cache", "Cache", "shadercache", "dev_flash", "dev_hdd0", "WpSystem", ".codex"
    };
    public static string[] CopyFiles(string root, CancellationToken token = default) {
        var files = new List<string>(); var folders = new Stack<string>(); folders.Push(root);
        while (folders.TryPop(out var folder)) {
            token.ThrowIfCancellationRequested();
            CollectionPaths.AssertNoLinks(root, folder);
            foreach (var entry in Directory.EnumerateFileSystemEntries(folder)) {
                token.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new UserError("A copy source contains a link or online-only placeholder. Make all source files locally available and use a folder without junctions.");
                if ((attributes & FileAttributes.Directory) != 0) folders.Push(entry); else files.Add(entry);
            }
        }
        return files.ToArray();
    }
    public static async Task<string> CopyVerifiedAsync(string source, string destination, IProgress<string>? progress = null, CancellationToken token = default) {
        source = Path.GetFullPath(source); destination = Path.GetFullPath(destination);
        if (File.Exists(destination) || Directory.Exists(destination)) throw new UserError("The destination already exists. Choose a new folder or filename.");
        if (CollectionPaths.IsWithin(source, destination) || source.Equals(destination, StringComparison.OrdinalIgnoreCase)) throw new UserError("The destination cannot be inside the source.");
        CollectionPaths.AssertNoLinks(Path.GetPathRoot(source)!, source);
        var isDirectory = Directory.Exists(source);
        var files = isDirectory ? CopyFiles(source, token) : File.Exists(source) ? [source] : throw new UserError("The source is no longer available.");
        long size = files.Sum(f => new FileInfo(f).Length);
        RequireSpace(destination, size);
        var destinationDrive = new DriveInfo(Path.GetPathRoot(destination)!);
        if (destinationDrive.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase) && files.Any(f => new FileInfo(f).Length > uint.MaxValue))
            throw new UserError("A source file exceeds FAT32's 4 GB file limit. Choose an existing exFAT or NTFS destination.");
        var staging = destination + ".copy-" + Guid.NewGuid().ToString("N");
        try {
            if (isDirectory) Directory.CreateDirectory(staging);
            foreach (var file in files) {
                token.ThrowIfCancellationRequested();
                var target = isDirectory ? CollectionPaths.SafeCombine(staging, Path.GetRelativePath(source, file)) : staging;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                progress?.Report($"Copying {Path.GetFileName(file)}");
                await using (var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true))
                await using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true))
                    await input.CopyToAsync(output, token);
                if (!Hash(file).Equals(Hash(target), StringComparison.Ordinal)) throw new UserError("Copy verification failed. The source was preserved.");
            }
            token.ThrowIfCancellationRequested();
            if (isDirectory) Directory.Move(staging, destination); else File.Move(staging, destination);
            return destination;
        } finally {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            if (File.Exists(staging)) File.Delete(staging);
        }
    }
}
