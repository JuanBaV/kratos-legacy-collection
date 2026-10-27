using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace KratosLegacyCollection.Core;

public sealed partial class GameInspector
{
    public Task<byte[]?> ReadArtworkAsync(string path, CancellationToken token = default) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        if (Directory.Exists(path))
        {
            var folder = Directory.Exists(Path.Combine(path, "PS3_GAME")) ? Path.Combine(path, "PS3_GAME") : path;
            foreach (var name in new[] { "PIC1.PNG", "ICON0.PNG" })
            {
                var file = Path.Combine(folder, name);
                if (File.Exists(file) && new FileInfo(file).Length is > 0 and <= ArtworkService.MaxBytes)
                    return File.ReadAllBytes(file);
            }
            return null;
        }
        if (!File.Exists(path) || !Path.GetExtension(path).Equals(".iso", StringComparison.OrdinalIgnoreCase))
            return null;
        using var stream = File.OpenRead(path);
        if (stream.Length < 17 * 2048)
            return null;
        var pvd = ReadAt(stream, 16 * 2048, 2048);
        if (pvd[0] != 1 || Encoding.ASCII.GetString(pvd, 1, 5) != "CD001")
            return null;
        var psp = FindRecord(stream, DirectoryRecord(pvd, 156), "PSP_GAME");
        if (psp == null)
            return null;
        foreach (var name in new[] { "PIC1.PNG", "ICON0.PNG" })
        {
            var entry = FindRecord(stream, psp.Value, name);
            if (entry is { size: > 0 and <= ArtworkService.MaxBytes })
                return ReadAt(stream, entry.Value.extent * 2048L, (int)entry.Value.size);
        }
        return null;
    }, token);
    public Task<GameInspection> InspectAsync(string path, CancellationToken token = default) => Task.Run(() => Inspect(path, token), token);
    private static GameInspection Inspect(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Directory.Exists(path))
        {
            var disc = Directory.Exists(Path.Combine(path, "PS3_GAME")) ? Path.Combine(path, "PS3_GAME") : path;
            var sfo = Path.Combine(disc, "PARAM.SFO");
            if (!File.Exists(sfo) || !File.Exists(Path.Combine(disc, "USRDIR", "EBOOT.BIN")))
                return new(path, null, null, null, false, "Select a PS3 disc folder containing PS3_GAME, PARAM.SFO and USRDIR/EBOOT.BIN.");
            var data = ReadSfo(File.ReadAllBytes(sfo));
            return FromSfo(path, data);
        }
        if (!File.Exists(path))
            return new(path, null, null, null, false, "The game files were not found.");
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".cso" or ".chd")
        {
            using var compressed = File.OpenRead(path);
            Span<byte> magic = stackalloc byte[8];
            if (compressed.Read(magic) < 8 || (extension == ".cso" ? Encoding.ASCII.GetString(magic[..4]) != "CISO" : Encoding.ASCII.GetString(magic) != "MComprHD"))
                return new(path, null, null, null, false, "The compressed image header is invalid.");
            return new(path, null, null, null, true, "Compressed image: confirm the Game ID from the emulator. Metadata cannot be inferred from a filename.");
        }
        if (extension != ".iso")
            return new(path, null, null, null, false, "Supported images: ISO, PSP CSO, PS2 CHD, or a PS3 disc folder.");
        using var stream = File.OpenRead(path);
        if (stream.Length < 17 * 2048)
            return new(path, null, null, null, false, "The file is too small to be an ISO image.");
        var pvd = ReadAt(stream, 16 * 2048, 2048);
        if (pvd[0] != 1 || Encoding.ASCII.GetString(pvd, 1, 5) != "CD001")
            return new(path, null, null, null, false, "The image does not contain a supported ISO9660 filesystem.");
        var root = DirectoryRecord(pvd, 156);
        var system = FindRecord(stream, root, "SYSTEM.CNF");
        if (system != null)
        {
            var content = Encoding.ASCII.GetString(ReadAt(stream, system.Value.extent * 2048L, (int)Math.Min(system.Value.size, 65536)));
            var idMatch = Ps2IdRegex().Match(content);
            if (idMatch.Success)
            {
                var id = Catalog.NormalizeId(idMatch.Value);
                return new(path, id, Catalog.Find(id)?.Name, null, true, "Game ID read from SYSTEM.CNF. Set ELF CRC from PCSX2 before importing CRC-specific patches.");
            }
        }
        var psp = FindRecord(stream, root, "PSP_GAME");
        var sfoRecord = psp == null ? null : FindRecord(stream, psp.Value, "PARAM.SFO");
        if (sfoRecord != null && sfoRecord.Value.size < 1024 * 1024)
            return FromSfo(path, ReadSfo(ReadAt(stream, sfoRecord.Value.extent * 2048L, (int)sfoRecord.Value.size)));
        return new(path, null, null, null, false, "No supported PS2 or PSP game identity was found. PS3 games must use an extracted disc folder.");
    }
    private static GameInspection FromSfo(string path, Dictionary<string, string> data)
    {
        data.TryGetValue("DISC_ID", out var id);
        if (id == null)
            data.TryGetValue("TITLE_ID", out id);
        data.TryGetValue("TITLE", out var title);
        data.TryGetValue("DISC_VERSION", out var revision);
        if (revision == null)
            data.TryGetValue("APP_VER", out revision);
        return new(path, id == null ? null : Catalog.NormalizeId(id), title, revision, id != null, id == null ? "PARAM.SFO has no game identifier." : "Game identity read from PARAM.SFO.");
    }
    public static Dictionary<string, string> ReadSfo(byte[] bytes)
    {
        var result = new Dictionary<string, string>();
        if (bytes.Length < 20 || bytes[0] != 0 || bytes[1] != 'P' || bytes[2] != 'S' || bytes[3] != 'F')
            throw new UserError("Invalid PARAM.SFO header.");
        uint Read(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
        var keys = Read(8);
        var values = Read(12);
        var count = Read(16);
        if (count > 4096 || 20L + count * 16L > bytes.Length)
            throw new UserError("Invalid PARAM.SFO index.");
        for (var i = 0; i < count; i++)
        {
            var offset = 20 + i * 16;
            long key = keys + (long)BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
            long value = values + (long)Read(offset + 12);
            long length = Read(offset + 4);
            if (key < 0 || key >= bytes.Length || value < 0 || length > 65536 || value + length > bytes.Length)
                throw new UserError("Invalid PARAM.SFO bounds.");
            var keyEnd = Array.IndexOf(bytes, (byte)0, (int)key);
            if (keyEnd < 0 || keyEnd - key > 128)
                throw new UserError("Invalid PARAM.SFO key.");
            result[Encoding.UTF8.GetString(bytes, (int)key, keyEnd - (int)key)] = Encoding.UTF8.GetString(bytes, (int)value, (int)length).TrimEnd('\0');
        }
        return result;
    }
    private static (uint extent, uint size) DirectoryRecord(byte[] buffer, int offset) =>
        (BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset + 2, 4)), BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset + 10, 4)));
    private static (uint extent, uint size)? FindRecord(FileStream stream, (uint extent, uint size) folder, string name)
    {
        if (folder.size > 16 * 1024 * 1024)
            throw new UserError("The ISO directory is too large to inspect safely.");
        var buffer = ReadAt(stream, folder.extent * 2048L, (int)folder.size);
        for (var i = 0; i < buffer.Length;)
        {
            if (buffer[i] == 0)
            {
                i = (i / 2048 + 1) * 2048;
                continue;
            }
            int length = buffer[i];
            if (length < 34 || i + length > buffer.Length || buffer[i + 32] > length - 33)
                throw new UserError("The ISO directory is malformed.");
            var entry = Encoding.ASCII.GetString(buffer, i + 33, buffer[i + 32]).Split(';')[0];
            if (entry.Equals(name, StringComparison.OrdinalIgnoreCase))
                return DirectoryRecord(buffer, i);
            i += length;
        }
        return null;
    }
    private static byte[] ReadAt(FileStream stream, long offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > stream.Length)
            throw new UserError("The image contains an invalid file extent.");
        var bytes = new byte[count];
        stream.Position = offset;
        stream.ReadExactly(bytes);
        return bytes;
    }
    [GeneratedRegex(@"S[CL][UEPK]S[_-]\d{3}\.\d{2}", RegexOptions.IgnoreCase)] private static partial Regex Ps2IdRegex();
}
