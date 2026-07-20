using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PowerKit.Extensions;

namespace MacBundle.Graphics;

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

    private void SaveIcnsToSeekable(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        var imagesBySize = Images
            .Where(i => i.Width == i.Height)
            .Where(i => TypeTagsBySize.ContainsKey(i.Width))
            .DistinctBy(i => i.Width)
            .ToDictionary(i => i.Width, i => i);

        if (!imagesBySize.Any())
        {
            throw new InvalidOperationException(
                "No supported icon sizes are available for the ICNS output."
            );
        }

        // -- Header

        // Magic
        stream.Write("icns"u8);

        // Length (will overwrite later)
        var lengthPortal = stream.CreatePortal();
        writer.WriteBigEndian(0u);

        foreach (var (size, image) in imagesBySize)
        {
            // -- Icon entry

            // Type
            stream.Write(TypeTagsBySize[size]);

            // Length (will overwrite later)
            var entryLengthPortal = stream.CreatePortal();
            writer.WriteBigEndian(0u);

            // Image data
            image.SavePng(stream);

            // Update length
            var entryLength = stream.Position - entryLengthPortal.Position + sizeof(uint);
            using (entryLengthPortal.Jump())
                writer.WriteBigEndian(checked((uint)entryLength));
        }

        // Update length
        var length = stream.Position - lengthPortal.Position + sizeof(uint);
        using (lengthPortal.Jump())
            writer.WriteBigEndian(checked((uint)length));
    }

    public void SaveIcns(Stream stream)
    {
        if (!stream.CanSeek)
        {
            using var seekableStream = new MemoryStream();
            SaveIcnsToSeekable(seekableStream);

            seekableStream.Position = 0;
            seekableStream.CopyTo(stream);
        }
        else
        {
            SaveIcnsToSeekable(stream);
        }
    }
}
