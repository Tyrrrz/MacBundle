using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace MacBundle;

internal sealed class Bitmap
{
    public int Width { get; }

    public int Height { get; }

    public byte[] Rgba32 { get; }

    public Bitmap(int width, int height, byte[] rgba32)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (rgba32.Length != width * height * 4)
            throw new ArgumentException("Unexpected pixel buffer size.", nameof(rgba32));

        Width = width;
        Height = height;
        Rgba32 = rgba32;
    }
}

internal sealed class MacIcons
{
    private static readonly byte[] IcnsMagic = { (byte)'i', (byte)'c', (byte)'n', (byte)'s' };
    private static readonly byte[] Ic10Type = { (byte)'i', (byte)'c', (byte)'1', (byte)'0' };

    private readonly byte[] _pngData;

    private MacIcons(byte[] pngData) => _pngData = pngData;

    public static MacIcons FromBitmap(Bitmap bitmap) => new(PngCodec.Encode(bitmap));

    public static MacIcons FromPngData(byte[] pngData) => new(pngData);

    public void Write(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        var payloadLength = checked(8u + (uint)_pngData.Length);
        var totalLength = checked(8u + payloadLength);

        writer.Write(IcnsMagic);
        WriteUInt32BigEndian(writer, totalLength);

        writer.Write(Ic10Type);
        WriteUInt32BigEndian(writer, payloadLength);
        writer.Write(_pngData);
    }

    private static void WriteUInt32BigEndian(BinaryWriter writer, uint value)
    {
        writer.Write((byte)(value >> 24));
        writer.Write((byte)(value >> 16));
        writer.Write((byte)(value >> 8));
        writer.Write((byte)value);
    }

    private static class PngCodec
    {
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        private static readonly byte[] IhdrType = { (byte)'I', (byte)'H', (byte)'D', (byte)'R' };
        private static readonly byte[] IdatType = { (byte)'I', (byte)'D', (byte)'A', (byte)'T' };
        private static readonly byte[] IendType = { (byte)'I', (byte)'E', (byte)'N', (byte)'D' };

        public static byte[] Encode(Bitmap bitmap)
        {
            using var rawDataStream = new MemoryStream();

            for (var y = 0; y < bitmap.Height; y++)
            {
                rawDataStream.WriteByte(0); // filter: none
                rawDataStream.Write(bitmap.Rgba32, y * bitmap.Width * 4, bitmap.Width * 4);
            }

            var rawData = rawDataStream.ToArray();
            var compressedData = Deflate(rawData);
            var adler32 = ComputeAdler32(rawData);

            using var idatDataStream = new MemoryStream();
            idatDataStream.WriteByte(0x78);
            idatDataStream.WriteByte(0x9C);
            idatDataStream.Write(compressedData, 0, compressedData.Length);
            WriteUInt32BigEndian(idatDataStream, adler32);

            var idatData = idatDataStream.ToArray();

            using var pngStream = new MemoryStream();
            using var writer = new BinaryWriter(pngStream, Encoding.UTF8, true);

            writer.Write(Signature);

            using var ihdrDataStream = new MemoryStream();
            WriteUInt32BigEndian(ihdrDataStream, checked((uint)bitmap.Width));
            WriteUInt32BigEndian(ihdrDataStream, checked((uint)bitmap.Height));
            ihdrDataStream.WriteByte(8); // bit depth
            ihdrDataStream.WriteByte(6); // color type RGBA
            ihdrDataStream.WriteByte(0); // compression
            ihdrDataStream.WriteByte(0); // filter
            ihdrDataStream.WriteByte(0); // interlace

            WriteChunk(writer, IhdrType, ihdrDataStream.ToArray());
            WriteChunk(writer, IdatType, idatData);
            WriteChunk(writer, IendType, Array.Empty<byte>());

            return pngStream.ToArray();
        }

        private static byte[] Deflate(byte[] data)
        {
            using var outputStream = new MemoryStream();
            using (var deflateStream = new DeflateStream(outputStream, CompressionLevel.Optimal, true))
                deflateStream.Write(data, 0, data.Length);

            return outputStream.ToArray();
        }

        private static void WriteChunk(BinaryWriter writer, byte[] type, byte[] data)
        {
            WriteUInt32BigEndian(writer.BaseStream, checked((uint)data.Length));
            writer.Write(type);
            writer.Write(data);

            var crc = ComputeCrc32(type, data);
            WriteUInt32BigEndian(writer.BaseStream, crc);
        }

        private static uint ComputeAdler32(byte[] data)
        {
            const uint mod = 65521;
            uint a = 1;
            uint b = 0;

            foreach (var value in data)
            {
                a = (a + value) % mod;
                b = (b + a) % mod;
            }

            return (b << 16) | a;
        }

        private static uint ComputeCrc32(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
        {
            var crc = 0xFFFFFFFFu;

            foreach (var b in first)
            {
                crc ^= b;
                for (var i = 0; i < 8; i++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }

            foreach (var b in second)
            {
                crc ^= b;
                for (var i = 0; i < 8; i++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }

            return ~crc;
        }

        private static void WriteUInt32BigEndian(Stream stream, uint value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }
    }
}
