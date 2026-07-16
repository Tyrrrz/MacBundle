using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace MacBundle;

internal partial class Icon
{
    private static readonly Dictionary<int, byte[]> TypeTagsBySize = new()
    {
        { 16, "icp4"u8.ToArray() },
        { 32, "icp5"u8.ToArray() },
        { 64, "icp6"u8.ToArray() },
        { 128, "ic07"u8.ToArray() },
        { 256, "ic08"u8.ToArray() },
        { 512, "ic09"u8.ToArray() },
        { 1024, "ic10"u8.ToArray() },
    };

    public void SaveIcns(Stream stream)
    {
        var pngBySize = new Dictionary<int, byte[]>();
        foreach (var bitmap in Bitmaps)
        {
            if (bitmap.Width != bitmap.Height)
                continue;

            if (!TypeTagsBySize.ContainsKey(bitmap.Width))
                continue;

            pngBySize[bitmap.Width] = bitmap.ToPngBytes();
        }

        var entries = new List<(byte[] typeTag, byte[] pngData)>();
        foreach (var pair in pngBySize)
        {
            if (TypeTagsBySize.TryGetValue(pair.Key, out var typeTag))
                entries.Add((typeTag, pair.Value));
        }

        if (entries.Count == 0)
            throw new InvalidOperationException(
                "No supported icon sizes are available for ICNS output."
            );

        var lengthBuffer = new byte[sizeof(uint)];

        var totalPayloadLength = 0u;
        foreach (var (_, pngData) in entries)
            totalPayloadLength = checked(totalPayloadLength + 8u + (uint)pngData.Length);

        var totalLength = checked(8u + totalPayloadLength);
        stream.Write("icns"u8);
        BinaryPrimitives.WriteUInt32BigEndian(lengthBuffer, totalLength);
        stream.Write(lengthBuffer);

        foreach (var (typeTag, pngData) in entries)
        {
            var payloadLength = checked(8u + (uint)pngData.Length);
            stream.Write(typeTag, 0, typeTag.Length);
            BinaryPrimitives.WriteUInt32BigEndian(lengthBuffer, payloadLength);
            stream.Write(lengthBuffer);
            stream.Write(pngData, 0, pngData.Length);
        }
    }
}
