using System.Text;

namespace PixelArtCli;

/// <summary>
/// Minimal PNG encoder for 8-bit RGBA images. No external compression library —
/// uses uncompressed ("stored") deflate blocks, which PNG's zlib wrapper permits.
/// Runs anywhere .NET runs, no GPU/display required.
/// </summary>
public static class PngWriter
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>
    /// Encode a width x height RGBA8 image (top-to-bottom, row-major, 4 bytes/pixel) as PNG bytes.
    /// </summary>
    public static byte[] Encode(int width, int height, byte[] rgba)
    {
        if (rgba.Length != width * height * 4)
            throw new ArgumentException($"rgba length {rgba.Length} does not match {width}x{height}x4");

        using var output = new MemoryStream();
        output.Write(Signature);

        WriteChunk(output, "IHDR", BuildIhdr(width, height));
        WriteChunk(output, "IDAT", BuildIdat(width, height, rgba));
        WriteChunk(output, "IEND", []);

        return output.ToArray();
    }

    public static void EncodeToFile(string path, int width, int height, byte[] rgba)
    {
        string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, Encode(width, height, rgba));
    }

    private static byte[] BuildIhdr(int width, int height)
    {
        var buf = new byte[13];
        WriteUInt32BE(buf, 0, (uint)width);
        WriteUInt32BE(buf, 4, (uint)height);
        buf[8] = 8;   // bit depth
        buf[9] = 6;   // color type: RGBA
        buf[10] = 0;  // compression method
        buf[11] = 0;  // filter method
        buf[12] = 0;  // interlace method
        return buf;
    }

    private static byte[] BuildIdat(int width, int height, byte[] rgba)
    {
        // Raw scanlines: each row prefixed with a filter-type byte (0 = None).
        int rowBytes = width * 4;
        var raw = new byte[height * (1 + rowBytes)];
        for (int y = 0; y < height; y++)
        {
            int rawOffset = y * (1 + rowBytes);
            raw[rawOffset] = 0; // filter: None
            Buffer.BlockCopy(rgba, y * rowBytes, raw, rawOffset + 1, rowBytes);
        }

        return ZlibWrapStored(raw);
    }

    /// <summary>
    /// Wrap raw bytes in a zlib stream using only uncompressed ("stored") deflate blocks.
    /// </summary>
    private static byte[] ZlibWrapStored(byte[] raw)
    {
        using var output = new MemoryStream();

        // zlib header: CMF=0x78 (deflate, 32K window), FLG=0x01 (no dict, check bits valid, level 0)
        output.WriteByte(0x78);
        output.WriteByte(0x01);

        const int maxBlockSize = 65535;
        int offset = 0;
        if (raw.Length == 0)
        {
            // Single empty final block.
            output.WriteByte(1); // BFINAL=1, BTYPE=00
            WriteUInt16LE(output, 0);
            WriteUInt16LE(output, 0xFFFF);
        }

        while (offset < raw.Length)
        {
            int blockSize = Math.Min(maxBlockSize, raw.Length - offset);
            bool isFinal = offset + blockSize >= raw.Length;

            output.WriteByte((byte)(isFinal ? 1 : 0)); // BFINAL, BTYPE=00 (stored)
            WriteUInt16LE(output, (ushort)blockSize);
            WriteUInt16LE(output, (ushort)~blockSize);
            output.Write(raw, offset, blockSize);

            offset += blockSize;
        }

        WriteUInt32BE(output, Adler32(raw));

        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        WriteUInt32BE(output, (uint)data.Length);

        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        uint crc = Crc32(typeBytes, data);
        WriteUInt32BE(output, crc);
    }

    private static void WriteUInt32BE(Stream s, uint value)
    {
        s.WriteByte((byte)(value >> 24));
        s.WriteByte((byte)(value >> 16));
        s.WriteByte((byte)(value >> 8));
        s.WriteByte((byte)value);
    }

    private static void WriteUInt32BE(byte[] buf, int offset, uint value)
    {
        buf[offset] = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }

    private static void WriteUInt16LE(Stream s, ushort value)
    {
        s.WriteByte((byte)value);
        s.WriteByte((byte)(value >> 8));
    }

    private static uint Adler32(byte[] data)
    {
        const uint modAdler = 65521;
        uint a = 1, b = 0;
        foreach (byte t in data)
        {
            a = (a + t) % modAdler;
            b = (b + a) % modAdler;
        }
        return (b << 16) | a;
    }

    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(byte[] typeBytes, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in typeBytes)
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (byte b in data)
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }
}
