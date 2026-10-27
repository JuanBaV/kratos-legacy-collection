namespace KratosLegacyCollection.Core;

public sealed class EmulatorProfiles(CollectionPaths paths)
{
    public async Task<string?> PrepareAsync(GameDefinition definition, GameSettings game, EmulatorSettings emulator, bool textures, bool cheats, CancellationToken token = default) {
        if (definition.Emulator == EmulatorKind.RPCS3) return null;
        if (definition.Emulator == EmulatorKind.PCSX2 && !SupportsPcsx2Overlay(emulator.Version)) {
            if (game.Preset != GraphicsPreset.Original || textures || cheats) throw new UserError("Automatic PCSX2 profiles require version 2.6 or newer. Configure this older emulator through its own interface.");
            return null;
        }
        var folder = Path.Combine(paths.Data, "Profiles", definition.Key); Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "launch.ini");
        if (definition.Emulator == EmulatorKind.PPSSPP) {
            var content = GraphicsProfiles.PspOverlay(game, textures);
            if (cheats) content += "[SystemParam]\n\n[General]\nEnableCheats = True\n";
            await File.WriteAllTextAsync(file, content, token);
        } else {
            var serial = game.GameId!.Insert(4, "-");
            var original = emulator.DataDirectory == null || game.Crc == null ? null : Path.Combine(paths.Resolve(emulator.DataDirectory), "gamesettings", serial + "_" + game.Crc + ".ini");
            var content = original != null && File.Exists(original) ? await File.ReadAllTextAsync(original, token) : "";
            content = IniSet(content, "EmuCore/GS", "upscale_multiplier", game.ResolutionScale.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (textures) content = IniSet(content, "EmuCore/GS", "LoadTextureReplacements", "true");
            if (game.Widescreen) content = IniSet(content, "EmuCore/GS", "AspectRatio", "16:9");
            if (cheats) content = IniSet(content, "EmuCore", "EnableCheats", "true");
            await File.WriteAllTextAsync(file, content, token);
        }
        return file;
    }
    public static bool SupportsPcsx2Overlay(string? version) => Version.TryParse(version?.TrimStart('v'), out var number) && number >= new Version(2, 6);
    public static string IniSet(string content, string section, string key, string value) {
        var lines = content.Replace("\r\n", "\n").Split('\n').ToList();
        var start = lines.FindIndex(l => l.Trim().Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase));
        if (start < 0) { lines.Add("[" + section + "]"); lines.Add(key + " = " + value); return string.Join('\n', lines); }
        var end = lines.FindIndex(start + 1, l => l.TrimStart().StartsWith('[')); if (end < 0) end = lines.Count;
        for (int i = start + 1; i < end; i++) {
            var equal = lines[i].IndexOf('=');
            if (equal > 0 && lines[i][..equal].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) { lines[i] = key + " = " + value; return string.Join('\n', lines); }
        }
        lines.Insert(end, key + " = " + value); return string.Join('\n', lines);
    }
}
