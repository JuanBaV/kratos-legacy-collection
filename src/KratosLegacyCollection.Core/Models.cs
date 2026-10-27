namespace KratosLegacyCollection.Core;

public enum EmulatorKind { PCSX2, PPSSPP, RPCS3 }
public enum GameStatus { ReadyToPlay, GameFilesRequired, EmulatorRequired, ConfigurationRequired, GameFilesNotFound, IncompatibleGameVersion, LaunchingGame }
public enum GraphicsPreset { Original, Enhanced, UltraHD, Custom }
public enum EnhancementKind { Textures, Performance, Display }

public sealed record GameDefinition(string Key, string Name, string Subtitle, string Platform, string Chapter,
    EmulatorKind Emulator, string[] GameIds, string Motif);

public static class Catalog
{
    public static readonly IReadOnlyList<GameDefinition> Games = [
        new("gow1", "God of War (2005)", "The mortal who defied Olympus", "PLAYSTATION 2", "I", EmulatorKind.PCSX2,
            ["SCUS97399", "SCES53133", "SCPS15090"], "temple"),
        new("gow2", "God of War II", "The fall of a god", "PLAYSTATION 2", "II", EmulatorKind.PCSX2,
            ["SCUS97481", "SCES54206", "SCES54794", "SCPS19210"], "blade"),
        new("chains", "God of War: Chains of Olympus", "Before the throne", "PLAYSTATION PORTABLE", "III", EmulatorKind.PPSSPP,
            ["UCUS98653", "UCES00842", "UCAS40198", "UCJS10086"], "chain"),
        new("ghost", "God of War: Ghost of Sparta", "A brother beyond the gods", "PLAYSTATION PORTABLE", "IV", EmulatorKind.PPSSPP,
            ["UCUS98737", "UCES01401", "UCAS40339", "UCJS10114"], "shield"),
        new("gow3", "God of War III", "Olympus will fall", "PLAYSTATION 3", "V", EmulatorKind.RPCS3,
            ["BCUS98111", "BCES00510", "BCES00799", "BCAS25003"], "ruin"),
        new("ascension", "God of War: Ascension", "Before the legend", "PLAYSTATION 3", "VI", EmulatorKind.RPCS3,
            ["BCUS98232", "BCES01741", "BCES01742", "BCAS25016"], "column")
    ];
    public static GameDefinition Get(string key) => Games.Single(g => g.Key == key);
    public static string NormalizeId(string value) => new(value.ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
    public static GameDefinition? Find(string id) => Games.FirstOrDefault(g => g.GameIds.Contains(NormalizeId(id)));
    public static string Region(string id) => NormalizeId(id) switch {
        var s when s.StartsWith("SCUS") || s.StartsWith("UCUS") || s.StartsWith("BCUS") => "North America",
        var s when s.StartsWith("SCES") || s.StartsWith("UCES") || s.StartsWith("BCES") => "Europe",
        var s when s.StartsWith("SCPS") || s.StartsWith("UCJS") => "Japan",
        var s when s.StartsWith("UCAS") || s.StartsWith("BCAS") => "Asia", _ => "Unknown"
    };
}

public sealed class LauncherSettings
{
    public int SchemaVersion { get; set; } = 1;
    public int SetupStep { get; set; }
    public bool SetupComplete { get; set; }
    public bool PortableMode { get; set; } = true;
    public bool ReducedMotion { get; set; }
    public bool Fullscreen { get; set; } = true;
    public Dictionary<EmulatorKind, EmulatorSettings> Emulators { get; set; } = [];
    public Dictionary<string, GameSettings> Games { get; set; } = [];
    public List<InstalledEnhancement> Enhancements { get; set; } = [];
    public string? CreatorDiscord { get; set; }
    public string? CreatorDiscordSource { get; set; }
    public static LauncherSettings Create() {
        var state = new LauncherSettings();
        foreach (var kind in Enum.GetValues<EmulatorKind>()) state.Emulators[kind] = new();
        foreach (var game in Catalog.Games) state.Games[game.Key] = new();
        return state;
    }
}

public sealed class EmulatorSettings
{
    public string? Executable { get; set; }
    public string? DataDirectory { get; set; }
    public string? Version { get; set; }
    public bool Prepared { get; set; }
    public bool PortableCopy { get; set; }
}

public sealed class GameSettings
{
    public string? Path { get; set; }
    public string? GameId { get; set; }
    public string? Revision { get; set; }
    public string? Crc { get; set; }
    public bool IdentityConfirmed { get; set; }
    public GraphicsPreset Preset { get; set; } = GraphicsPreset.Original;
    public int ResolutionScale { get; set; } = 1;
    public string Renderer { get; set; } = "Default";
    public bool Widescreen { get; set; }
}

public sealed class InstalledEnhancement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string GameKey { get; set; } = "";
    public string GameId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "User supplied";
    public string Version { get; set; } = "Unspecified";
    public string? Page { get; set; }
    public EnhancementKind Kind { get; set; }
    public string PayloadDirectory { get; set; } = "";
    public string TargetDirectory { get; set; } = "";
    public bool Enabled { get; set; }
    public List<OwnedFile> Files { get; set; } = [];
}

public sealed class OwnedFile
{
    public string RelativePath { get; set; } = "";
    public string Hash { get; set; } = "";
    public string? BackupPath { get; set; }
}

public sealed record EmulatorCandidate(EmulatorKind Kind, string Executable, string? Version, string? DataDirectory);
public sealed record GameInspection(string Path, string? GameId, string? Title, string? Revision, bool Supported, string Message);
public sealed record LaunchPlan(string Executable, string WorkingDirectory, IReadOnlyList<string> Arguments);
public sealed class UserError(string message) : Exception(message);
