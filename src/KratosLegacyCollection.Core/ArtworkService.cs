using System.Buffers.Binary;
using System.Security.Cryptography;

namespace KratosLegacyCollection.Core;

public sealed class ArtworkService(CollectionPaths paths)
{
    public const int MaxBytes = 12 * 1024 * 1024;
    public async Task<string> SaveAsync(GameDefinition game, byte[] bytes, CancellationToken token = default) {
        var extension = Validate(bytes);
        var folder = Path.Combine(paths.Data, "Artwork", game.Key); Directory.CreateDirectory(folder);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var file = CollectionPaths.SafeCombine(folder, hash + extension);
        if (!File.Exists(file)) await File.WriteAllBytesAsync(file, bytes, token);
        return paths.Store(file);
    }
    public static string Validate(byte[] bytes) {
        if (bytes.Length is < 24 or > MaxBytes) throw new UserError("Choose a PNG or JPEG image smaller than 12 MB.");
        var png = bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff;
        if (!png && !jpeg) throw new UserError("The selected file is not a PNG or JPEG image.");
        if (png) {
            var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16)); var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20));
            CheckDimensions(width, height); return ".png";
        }
        for (int position = 2; position + 4 < bytes.Length;) {
            if (bytes[position++] != 0xff) throw new UserError("The JPEG image header is invalid.");
            while (position < bytes.Length && bytes[position] == 0xff) position++;
            if (position >= bytes.Length) break;
            var marker = bytes[position++];
            if (marker is 0xd8 or >= 0xd0 and <= 0xd7) continue;
            if (marker is 0xda or 0xd9 || position + 2 > bytes.Length) break;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position));
            if (length < 2 || position + length > bytes.Length) break;
            if (marker is >= 0xc0 and <= 0xcf && marker is not (0xc4 or 0xc8 or 0xcc) && length >= 8) {
                var height = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position + 3)); var width = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position + 5));
                CheckDimensions(width, height); return ".jpg";
            }
            position += length;
        }
        throw new UserError("The JPEG image does not contain supported dimensions.");
    }
    private static void CheckDimensions(uint width, uint height) {
        if (width is 0 or > 8192 || height is 0 or > 8192 || (ulong)width * height > 32_000_000) throw new UserError("The artwork image exceeds the supported pixel dimensions.");
    }
}
