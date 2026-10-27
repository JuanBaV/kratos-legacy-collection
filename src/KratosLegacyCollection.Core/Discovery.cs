using System.Diagnostics;

namespace KratosLegacyCollection.Core;

public sealed class EmulatorDiscovery
{
    public static readonly IReadOnlyDictionary<EmulatorKind, string[]> Names = new Dictionary<EmulatorKind, string[]> {
        [EmulatorKind.PCSX2] = ["pcsx2-qt.exe", "pcsx2.exe", "pcsx2-qtx64-avx2.exe", "pcsx2-qtx64-sse4.exe"],
        [EmulatorKind.PPSSPP] = ["PPSSPPWindows64.exe", "PPSSPPWindows.exe"], [EmulatorKind.RPCS3] = ["rpcs3.exe"]
    };
    public static readonly IReadOnlyDictionary<EmulatorKind, string> DownloadPages = new Dictionary<EmulatorKind, string> {
        [EmulatorKind.PCSX2] = "https://pcsx2.net/download/", [EmulatorKind.PPSSPP] = "https://www.ppsspp.org/download/", [EmulatorKind.RPCS3] = "https://rpcs3.net/download"
    };
    public static string? DefaultData(EmulatorKind kind, string exe) {
        var parent = Path.GetDirectoryName(exe)!;
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return kind switch {
            EmulatorKind.PCSX2 => File.Exists(Path.Combine(parent, "portable.ini")) || File.Exists(Path.Combine(parent, "portable.txt")) ? parent : Path.Combine(documents, "PCSX2"),
            EmulatorKind.PPSSPP => Directory.Exists(Path.Combine(parent, "memstick")) ? Path.Combine(parent, "memstick") : Directory.Exists(Path.Combine(documents, "PPSSPP", "PSP")) ? Path.Combine(documents, "PPSSPP") : Path.Combine(parent, "memstick"),
            _ => parent
        };
    }
    public static EmulatorCandidate Validate(EmulatorKind kind, string path) {
        if (!File.Exists(path)) throw new UserError("The emulator executable was not found.");
        if (!Names[kind].Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)) throw new UserError($"Select a supported {kind} Windows executable.");
        using (var stream = File.OpenRead(path)) {
            if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z') throw new UserError("The selected file is not a Windows executable.");
        }
        var version = FileVersionInfo.GetVersionInfo(path).FileVersion;
        return new(kind, Path.GetFullPath(path), version, DefaultData(kind, path));
    }
    public static IEnumerable<string> UsualRoots() {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Path.Combine(home, "Downloads"), Path.Combine(home, "Desktop"), Path.Combine(documents, "Emulators"), Path.Combine(home, "Games"), @"C:\Emulators", @"C:\Games" })
            if (Directory.Exists(root)) yield return root;
    }
    public Task<IReadOnlyList<EmulatorCandidate>> FindAsync(IEnumerable<string> roots, IProgress<string>? progress = null, CancellationToken token = default) => Task.Run<IReadOnlyList<EmulatorCandidate>>(() => {
        var found = new Dictionary<string, EmulatorCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase)) {
            progress?.Report($"Searching {Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar))}");
            foreach (var file in StorageOperations.EnumerateSafe(root, 5, token)) {
                foreach (var pair in Names) {
                    if (!pair.Value.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)) continue;
                    try { found[file] = Validate(pair.Key, file); } catch (UserError) { } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
        }
        return found.Values.ToArray();
    }, token);
}

public sealed class GameDiscovery(GameInspector inspector)
{
    public Task<IReadOnlyList<GameInspection>> FindAsync(string root, IProgress<string>? progress = null, CancellationToken token = default) => Task.Run<IReadOnlyList<GameInspection>>(async () => {
        var found = new List<GameInspection>();
        foreach (var file in StorageOperations.EnumerateSafe(root, 12, token)) {
            var extension = Path.GetExtension(file).ToLowerInvariant();
            if (extension is not ".iso" and not ".cso" and not ".chd" && !Path.GetFileName(file).Equals("PARAM.SFO", StringComparison.OrdinalIgnoreCase)) continue;
            progress?.Report($"Inspecting {Path.GetFileName(file)}");
            try {
                var candidate = Path.GetFileName(file).Equals("PARAM.SFO", StringComparison.OrdinalIgnoreCase) && string.Equals(Path.GetFileName(Path.GetDirectoryName(file)), "PS3_GAME", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetDirectoryName(Path.GetDirectoryName(file))! : file;
                var metadata = await inspector.InspectAsync(candidate, token);
                if (metadata.GameId != null && Catalog.Find(metadata.GameId) != null) found.Add(metadata);
            } catch (IOException) { } catch (UserError) { } catch (UnauthorizedAccessException) { }
        }
        return found;
    }, token);
}
