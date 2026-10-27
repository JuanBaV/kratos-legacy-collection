using System.Diagnostics;

namespace KratosLegacyCollection.Core;

public sealed class LaunchService(CollectionPaths paths)
{
    public GameStatus Status(GameDefinition definition, LauncherSettings state) {
        var game = state.Games[definition.Key];
        if (string.IsNullOrEmpty(game.Path)) return GameStatus.GameFilesRequired;
        var path = paths.Resolve(game.Path);
        if (!File.Exists(path) && !Directory.Exists(path)) return GameStatus.GameFilesNotFound;
        if (!game.IdentityConfirmed || game.GameId == null || !definition.GameIds.Contains(Catalog.NormalizeId(game.GameId))) return GameStatus.IncompatibleGameVersion;
        var emulator = state.Emulators[definition.Emulator];
        if (emulator.Executable == null || !File.Exists(paths.Resolve(emulator.Executable))) return GameStatus.EmulatorRequired;
        if (!emulator.Prepared) return GameStatus.ConfigurationRequired;
        return GameStatus.ReadyToPlay;
    }
    public LaunchPlan Plan(GameDefinition definition, LauncherSettings state, string? profile = null) {
        var status = Status(definition, state);
        if (status != GameStatus.ReadyToPlay) throw new UserError($"Cannot launch: {Label(status)}. Open this game's settings.");
        var emulator = state.Emulators[definition.Emulator];
        var executable = paths.Resolve(emulator.Executable!);
        EmulatorDiscovery.Validate(definition.Emulator, executable);
        var game = paths.Resolve(state.Games[definition.Key].Path!);
        var arguments = new List<string>();
        switch (definition.Emulator) {
            case EmulatorKind.PCSX2:
                arguments.Add("-batch"); if (state.Fullscreen) arguments.Add("-fullscreen");
                if (emulator.PortableCopy) arguments.Add("-portable");
                else if (emulator.DataDirectory != null && EmulatorProfiles.SupportsPcsx2Overlay(emulator.Version)) { arguments.Add("-datapath"); arguments.Add(paths.Resolve(emulator.DataDirectory)); }
                if (profile != null) { arguments.Add("-gamecfg"); arguments.Add(profile); }
                arguments.Add("--"); arguments.Add(game); break;
            case EmulatorKind.PPSSPP:
                if (state.Fullscreen) arguments.Add("--fullscreen");
                if (profile != null) arguments.Add("--appendconfig=" + profile);
                arguments.Add(game); break;
            case EmulatorKind.RPCS3:
                var disc = Directory.Exists(Path.Combine(game, "PS3_GAME")) ? Path.Combine(game, "PS3_GAME") : game;
                var boot = Path.Combine(disc, "USRDIR", "EBOOT.BIN");
                if (!File.Exists(boot)) throw new UserError("The PS3 boot file was not found.");
                arguments.Add("--no-gui"); if (state.Fullscreen) arguments.Add("--fullscreen");
                if (profile != null) { arguments.Add("--config"); arguments.Add(profile); }
                arguments.Add(boot); break;
        }
        return new(executable, Path.GetDirectoryName(executable)!, arguments);
    }
    public static Process Start(LaunchPlan plan) {
        var start = new ProcessStartInfo(plan.Executable) { WorkingDirectory = plan.WorkingDirectory, UseShellExecute = false };
        foreach (var argument in plan.Arguments) start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new UserError("Windows could not start the emulator.");
    }
    public static string Label(GameStatus status) => status switch {
        GameStatus.ReadyToPlay => "READY TO PLAY", GameStatus.GameFilesRequired => "GAME FILES REQUIRED", GameStatus.EmulatorRequired => "EMULATOR REQUIRED",
        GameStatus.ConfigurationRequired => "CONFIGURATION REQUIRED", GameStatus.GameFilesNotFound => "GAME FILES NOT FOUND", GameStatus.IncompatibleGameVersion => "INCOMPATIBLE GAME VERSION", _ => "LAUNCHING GAME"
    };
}

public static class GraphicsProfiles
{
    public static void Select(GameSettings game, GraphicsPreset preset) {
        game.Preset = preset;
        game.UseLaunchProfile = true;
        if (preset != GraphicsPreset.Custom) { game.ResolutionScale = preset switch { GraphicsPreset.Original => 1, GraphicsPreset.Enhanced => 3, _ => 6 }; game.Renderer = "Default"; }
    }
    public static string PspOverlay(GameSettings game, bool textures) {
        if (game.ResolutionScale is < 1 or > 10) throw new UserError("Resolution scale must be between 1 and 10.");
        var backend = game.Renderer switch { "Vulkan" => "\nGraphicsBackend = 3", "Direct3D 11" => "\nGraphicsBackend = 2", "OpenGL" => "\nGraphicsBackend = 0", _ => "" };
        return $"[Graphics]\n{(game.UseLaunchProfile ? "InternalResolution = " + game.ResolutionScale + backend : "")}{(textures ? "\nReplaceTextures = True" : "")}\n";
    }
}
