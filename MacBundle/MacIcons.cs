using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace MacBundle;

internal class MacIcons
{
    private static readonly byte[] IcnsMagic = [(byte)'i', (byte)'c', (byte)'n', (byte)'s'];

    // Maps square pixel size to the corresponding ICNS type tag
    private static readonly Dictionary<int, byte[]> TypeTagBySize = new()
    {
        { 16, "icp4"u8.ToArray() },
        { 32, "icp5"u8.ToArray() },
        { 64, "icp6"u8.ToArray() },
        { 128, "ic07"u8.ToArray() },
        { 256, "ic08"u8.ToArray() },
        { 512, "ic09"u8.ToArray() },
        { 1024, "ic10"u8.ToArray() },
    };

    private readonly List<(byte[] typeTag, byte[] pngData)> _entries;

    private MacIcons(List<(byte[] typeTag, byte[] pngData)> entries) => _entries = entries;

    // Creates a MacIcons from multiple size-indexed PNG entries.
    // Sizes that don't map to a known ICNS type tag are silently skipped.
    // Returns null if none of the provided sizes are supported.
    public static MacIcons? FromSizedImages(Dictionary<int, byte[]> pngBySize)
    {
        var entries = new List<(byte[] typeTag, byte[] pngData)>();
        foreach (var pair in pngBySize)
        {
            if (TypeTagBySize.TryGetValue(pair.Key, out var typeTag))
                entries.Add((typeTag, pair.Value));
        }

        return entries.Count > 0 ? new MacIcons(entries) : null;
    }

    // Encodes a Bitmap to PNG bytes; used by IcnsWriter when an ICO entry is a raw bitmap
    internal static byte[] EncodeBitmapToPng(Bitmap bitmap) => PngCodec.Encode(bitmap);

    public void Write(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        var totalPayloadLength = 0u;
        foreach (var entry in _entries)
            totalPayloadLength = checked(totalPayloadLength + 8u + (uint)entry.pngData.Length);

        var totalLength = checked(8u + totalPayloadLength);
        writer.Write(IcnsMagic);
        WriteUInt32BigEndian(writer, totalLength);

        foreach (var entry in _entries)
        {
            var payloadLength = checked(8u + (uint)entry.pngData.Length);
            writer.Write(entry.typeTag);
            WriteUInt32BigEndian(writer, payloadLength);
            writer.Write(entry.pngData);
        }
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
        private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
        private static readonly byte[] IhdrType = [(byte)'I', (byte)'H', (byte)'D', (byte)'R'];
        private static readonly byte[] IdatType = [(byte)'I', (byte)'D', (byte)'A', (byte)'T'];
        private static readonly byte[] IendType = [(byte)'I', (byte)'E', (byte)'N', (byte)'D'];

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
            using (
                var deflateStream = new DeflateStream(outputStream, CompressionLevel.Optimal, true)
            )
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
