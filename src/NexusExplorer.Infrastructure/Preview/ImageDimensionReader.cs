using System.Buffers.Binary;

namespace NexusExplorer.Infrastructure.Preview;

/// <summary>
/// Reads pixel dimensions straight from an image file's header without decoding the
/// whole bitmap. Supports PNG, JPEG, GIF, BMP and WebP (VP8/VP8L/VP8X). Returns
/// (null, null) for anything it can't parse (e.g. SVG, which is vector).
/// </summary>
internal static class ImageDimensionReader
{
    public static (int? Width, int? Height) TryRead(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[32];
            var read = stream.Read(header);
            if (read < 12) return (null, null);

            // PNG: 89 50 4E 47 0D 0A 1A 0A, IHDR width/height at bytes 16..24 (big-endian).
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                if (read >= 24)
                {
                    var w = BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4));
                    var h = BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4));
                    return (w, h);
                }
                return (null, null);
            }

            // GIF: "GIF87a"/"GIF89a", logical screen width/height at bytes 6..10 (little-endian).
            if (header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F')
            {
                var w = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(6, 2));
                var h = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(8, 2));
                return (w, h);
            }

            // BMP: "BM", DIB header width/height at bytes 18..26 (little-endian).
            if (header[0] == (byte)'B' && header[1] == (byte)'M' && read >= 26)
            {
                var w = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(18, 4));
                var h = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(22, 4));
                return (w, Math.Abs(h));
            }

            // WebP: "RIFF"...."WEBP"
            if (header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
                && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
            {
                return ReadWebP(stream);
            }

            // JPEG: FF D8 — must scan segments for a Start-Of-Frame marker.
            if (header[0] == 0xFF && header[1] == 0xD8)
            {
                return ReadJpeg(stream);
            }

            return (null, null);
        }
        catch
        {
            return (null, null);
        }
    }

    private static (int? Width, int? Height) ReadWebP(FileStream stream)
    {
        // We already consumed the first 32 bytes into a local buffer; re-read from the
        // VP8 chunk which starts at offset 12.
        stream.Position = 12;
        Span<byte> chunk = stackalloc byte[18];
        if (stream.Read(chunk) < 18) return (null, null);

        // chunk[0..4] = fourCC ("VP8 ", "VP8L", "VP8X")
        if (chunk[0] == 'V' && chunk[1] == 'P' && chunk[2] == '8' && chunk[3] == ' ')
        {
            // Lossy: after 4CC(4) + size(4) + frame tag(3) + start code(3) come 16-bit w/h.
            var w = BinaryPrimitives.ReadUInt16LittleEndian(chunk.Slice(14, 2)) & 0x3FFF;
            // Need two more bytes for height.
            Span<byte> more = stackalloc byte[2];
            if (stream.Read(more) < 2) return (w, null);
            var h = BinaryPrimitives.ReadUInt16LittleEndian(more) & 0x3FFF;
            return (w, h);
        }
        if (chunk[0] == 'V' && chunk[1] == 'P' && chunk[2] == '8' && chunk[3] == 'X')
        {
            // Extended: 24-bit (w-1) and (h-1) little-endian at offsets 12 and 15.
            var w = (chunk[12] | (chunk[13] << 8) | (chunk[14] << 16)) + 1;
            var h = (chunk[15] | (chunk[16] << 8) | (chunk[17] << 16)) + 1;
            return (w, h);
        }
        if (chunk[0] == 'V' && chunk[1] == 'P' && chunk[2] == '8' && chunk[3] == 'L')
        {
            // Lossless: 1 signature byte then 14-bit (w-1) and (h-1) packed.
            var b0 = chunk[9];
            var b1 = chunk[10];
            var b2 = chunk[11];
            var b3 = chunk[12];
            var w = ((b1 & 0x3F) << 8 | b0) + 1;
            var h = ((b3 & 0x0F) << 10 | b2 << 2 | (b1 & 0xC0) >> 6) + 1;
            return (w, h);
        }
        return (null, null);
    }

    private static (int? Width, int? Height) ReadJpeg(FileStream stream)
    {
        stream.Position = 2; // past the FF D8 SOI marker
        Span<byte> buf = stackalloc byte[8];

        while (true)
        {
            // Find the next marker: a 0xFF byte followed by a non-0xFF, non-0x00 marker id.
            int b = stream.ReadByte();
            if (b < 0) return (null, null);
            if (b != 0xFF) continue;

            int marker;
            do
            {
                marker = stream.ReadByte();
                if (marker < 0) return (null, null);
            } while (marker == 0xFF);

            // Standalone markers without a length payload.
            if (marker is 0xD8 or 0xD9 || (marker >= 0xD0 && marker <= 0xD7)) continue;

            // Segment length (includes the 2 length bytes).
            if (stream.Read(buf.Slice(0, 2)) < 2) return (null, null);
            var segLength = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(0, 2));
            if (segLength < 2) return (null, null);

            // Start-Of-Frame markers carry the image dimensions.
            var isSof = marker is >= 0xC0 and <= 0xCF
                        && marker is not (0xC4 or 0xC8 or 0xCC);
            if (isSof)
            {
                // SOF payload: precision(1), height(2), width(2), ...
                if (stream.Read(buf.Slice(0, 5)) < 5) return (null, null);
                var h = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(1, 2));
                var w = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(3, 2));
                return (w, h);
            }

            // Skip this segment's payload.
            stream.Position += segLength - 2;
        }
    }
}
