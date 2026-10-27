namespace KratosLegacyCollection.Core;

public sealed class EmulatorProfiles(CollectionPaths paths)
{
    public async Task<string?> PrepareAsync(GameDefinition definition, GameSettings game, EmulatorSettings emulator, bool textures, bool cheats, CancellationToken token = default)
    {
        if (!game.UseLaunchProfile && !textures && !cheats)
            return null;
        if (game.ResolutionScale is < 1 or > 10)
            throw new UserError("Resolution scale must be between 1 and 10.");
        if (definition.Emulator == EmulatorKind.RPCS3)
            return await PrepareRpcAsync(game, definition, emulator, token);
        if (definition.Emulator == EmulatorKind.PCSX2 && !SupportsPcsx2Overlay(emulator.Version))
        {
            if (game.Preset != GraphicsPreset.Original || textures || cheats)
                throw new UserError("Automatic PCSX2 profiles require version 2.6 or newer. Configure this older emulator through its own interface.");
            return null;
        }
        var folder = Path.Combine(paths.Data, "Profiles", definition.Key);
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "launch.ini");
        if (definition.Emulator == EmulatorKind.PPSSPP)
        {
            var content = GraphicsProfiles.PspOverlay(game, textures);
            if (cheats)
                content = IniSet(content, "General", "EnableCheats", "True");
            await File.WriteAllTextAsync(file, content, token);
        }
        else
        {
            var serial = game.GameId!.Insert(4, "-");
            var original = emulator.DataDirectory == null || game.Crc == null ? null : Path.Combine(paths.Resolve(emulator.DataDirectory), "gamesettings", serial + "_" + game.Crc + ".ini");
            var content = original != null && File.Exists(original) ? await File.ReadAllTextAsync(original, token) : "";
            if (game.UseLaunchProfile)
                content = IniSet(content, "EmuCore/GS", "upscale_multiplier", game.ResolutionScale.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (textures)
                content = IniSet(content, "EmuCore/GS", "LoadTextureReplacements", "true");
            if (game.Widescreen)
                content = IniSet(content, "EmuCore/GS", "AspectRatio", "16:9");
            if (cheats)
                content = IniSet(content, "EmuCore", "EnableCheats", "true");
            await File.WriteAllTextAsync(file, content, token);
        }
        return file;
    }
    private async Task<string> PrepareRpcAsync(GameSettings game, GameDefinition definition, EmulatorSettings emulator, CancellationToken token)
    {
        if (game.Renderer is not ("Default" or "Vulkan" or "OpenGL"))
            throw new UserError("Choose Default, Vulkan or OpenGL for RPCS3.");
        if (emulator.DataDirectory == null)
            throw new UserError("Select RPCS3's data folder before applying a graphics profile.");
        var data = paths.Resolve(emulator.DataDirectory);
        var custom = Path.Combine(data, "config", "custom_configs", "config_" + game.GameId + ".yml");
        var global = File.Exists(Path.Combine(data, "config", "config.yml")) ? Path.Combine(data, "config", "config.yml") : Path.Combine(data, "config.yml");
        var source = File.Exists(custom) ? custom : global;
        if (!File.Exists(source))
            throw new UserError("RPCS3's configuration was not found. Open RPCS3 and configure it first, or turn off the launch profile.");
        var content = await File.ReadAllTextAsync(source, token);
        content = YamlScalar(content, "Video", "Resolution Scale", (Math.Min(game.ResolutionScale, 8) * 100).ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (game.Renderer is "Vulkan" or "OpenGL")
            content = YamlScalar(content, "Video", "Renderer", game.Renderer);
        var folder = Path.Combine(paths.Data, "Profiles", definition.Key);
        Directory.CreateDirectory(folder);
        var destination = Path.Combine(folder, "launch.yml");
        await File.WriteAllTextAsync(destination, content, token);
        return destination;
    }
    public static string YamlScalar(string content, string section, string key, string value)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n').ToList();
        var start = lines.FindIndex(l => l == section + ":");
        if (start < 0 || lines.Count(l => l == section + ":") != 1)
            throw new UserError("This RPCS3 configuration layout is unsupported. Use RPCS3's native game settings.");
        var end = lines.FindIndex(start + 1, l => l.Length > 0 && !char.IsWhiteSpace(l[0]) && !l.StartsWith('#'));
        if (end < 0)
            end = lines.Count;
        var matches = Enumerable.Range(start + 1, end - start - 1).Where(i => lines[i].StartsWith("  " + key + ":", StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
            throw new UserError("A required RPCS3 graphics setting is missing or duplicated. Use RPCS3's native game settings.");
        lines[matches[0]] = "  " + key + ": " + value;
        return string.Join('\n', lines);
    }
    public static bool SupportsPcsx2Overlay(string? version) => Version.TryParse(version?.TrimStart('v'), out var number) && number >= new Version(2, 6);
    public static string IniSet(string content, string section, string key, string value)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n').ToList();
        var start = lines.FindIndex(l => l.Trim().Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase));
        if (start < 0)
        {
            lines.Add("[" + section + "]");
            lines.Add(key + " = " + value);
            return string.Join('\n', lines);
        }
        var end = lines.FindIndex(start + 1, l => l.TrimStart().StartsWith('['));
        if (end < 0)
            end = lines.Count;
        for (int i = start + 1; i < end; i++)
        {
            var equal = lines[i].IndexOf('=');
            if (equal > 0 && lines[i][..equal].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = key + " = " + value;
                return string.Join('\n', lines);
            }
        }
        lines.Insert(end, key + " = " + value);
        return string.Join('\n', lines);
    }
}
