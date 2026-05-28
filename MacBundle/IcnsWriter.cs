using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MacBundle;

internal static class IcnsWriter
{
    private const uint IcnsHeaderSize = 8;
    private const uint ChunkHeaderSize = 8;
    private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
    private static readonly byte[] IcnsMagic = { (byte)'i', (byte)'c', (byte)'n', (byte)'s' };
    private static readonly byte[] Ic10Type = { (byte)'i', (byte)'c', (byte)'1', (byte)'0' };

    public static bool TryCreateFromImage(
        string sourceImagePath,
        string targetIcnsPath,
        Action<string>? logWarning
    )
    {
        if (string.Equals(Path.GetExtension(sourceImagePath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            return TryCreateFromPngStream(
                File.OpenRead(sourceImagePath),
                new FileInfo(sourceImagePath).Length,
                sourceImagePath,
                targetIcnsPath,
                logWarning
            );
        }

        if (string.Equals(Path.GetExtension(sourceImagePath), ".ico", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryExtractPngDataFromIco(sourceImagePath, out var pngData, logWarning))
                return false;

            return TryCreateFromPngStream(
                new MemoryStream(pngData),
                pngData.LongLength,
                sourceImagePath,
                targetIcnsPath,
                logWarning
            );
        }

        logWarning?.Invoke(
            string.Format(
                CultureInfo.InvariantCulture,
                "ApplicationIcon '{0}' is not a .png, .ico, or .icns file.",
                sourceImagePath
            )
        );
        return false;
    }

    private static bool TryCreateFromPngStream(
        Stream sourcePngStream,
        long sourcePngLength,
        string sourceImagePath,
        string targetIcnsPath,
        Action<string>? logWarning
    )
    {
        using (sourcePngStream)
        {
            if (sourcePngLength < PngSignature.Length || sourcePngLength > uint.MaxValue)
            {
                logWarning?.Invoke(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ApplicationIcon '{0}' is not a valid PNG image.",
                        sourceImagePath
                    )
                );
                return false;
            }

            var signatureBuffer = new byte[PngSignature.Length];
            if (
                sourcePngStream.Read(signatureBuffer, 0, signatureBuffer.Length) != signatureBuffer.Length
                || !signatureBuffer.SequenceEqual(PngSignature)
            )
            {
                logWarning?.Invoke(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ApplicationIcon '{0}' is not a valid PNG image.",
                        sourceImagePath
                    )
                );
                return false;
            }

            var pngLength = checked((uint)sourcePngLength);
            var payloadLength = checked(ChunkHeaderSize + pngLength);
            var totalLength = checked(IcnsHeaderSize + payloadLength);

            using var stream = File.Create(targetIcnsPath);
            using var writer = new BinaryWriter(stream);

            writer.Write(IcnsMagic);
            WriteUInt32BigEndian(writer, totalLength);

            writer.Write(Ic10Type);
            WriteUInt32BigEndian(writer, payloadLength);

            sourcePngStream.Position = 0;
            sourcePngStream.CopyTo(stream);
        }

        return true;
    }

    private static bool TryExtractPngDataFromIco(
        string sourceImagePath,
        out byte[] pngData,
        Action<string>? logWarning
    )
    {
        pngData = Array.Empty<byte>();

        var icoData = File.ReadAllBytes(sourceImagePath);
        if (
            icoData.Length < 6
            || ReadUInt16LittleEndian(icoData, 0) != 0
            || ReadUInt16LittleEndian(icoData, 2) != 1
        )
        {
            logWarning?.Invoke(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ApplicationIcon '{0}' is not a valid ICO image.",
                    sourceImagePath
                )
            );
            return false;
        }

        var entryCount = ReadUInt16LittleEndian(icoData, 4);
        if (entryCount == 0 || icoData.Length < 6 + entryCount * 16)
        {
            logWarning?.Invoke(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ApplicationIcon '{0}' is not a valid ICO image.",
                    sourceImagePath
                )
            );
            return false;
        }

        var selectedOffset = -1;
        var selectedLength = 0;
        var selectedArea = -1;

        for (var i = 0; i < entryCount; i++)
        {
            var entryOffset = 6 + i * 16;
            var width = icoData[entryOffset] == 0 ? 256 : icoData[entryOffset];
            var height = icoData[entryOffset + 1] == 0 ? 256 : icoData[entryOffset + 1];

            var bytesInRes = (int)ReadUInt32LittleEndian(icoData, entryOffset + 8);
            var imageOffset = (int)ReadUInt32LittleEndian(icoData, entryOffset + 12);
            if (
                bytesInRes < PngSignature.Length
                || imageOffset < 0
                || imageOffset > icoData.Length - bytesInRes
            )
            {
                continue;
            }

            if (!HasPngSignature(icoData, imageOffset))
                continue;

            var area = width * height;
            if (area < selectedArea)
                continue;

            selectedArea = area;
            selectedOffset = imageOffset;
            selectedLength = bytesInRes;
        }

        if (selectedOffset < 0 || selectedLength <= 0)
        {
            logWarning?.Invoke(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ApplicationIcon '{0}' does not contain PNG-encoded icon entries.",
                    sourceImagePath
                )
            );
            return false;
        }

        pngData = new byte[selectedLength];
        Buffer.BlockCopy(icoData, selectedOffset, pngData, 0, selectedLength);

        return true;
    }

    private static ushort ReadUInt16LittleEndian(byte[] data, int offset) =>
        (ushort)(data[offset] | (data[offset + 1] << 8));

    private static uint ReadUInt32LittleEndian(byte[] data, int offset) =>
        (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

    private static bool HasPngSignature(byte[] data, int offset)
    {
        for (var i = 0; i < PngSignature.Length; i++)
        {
            if (data[offset + i] != PngSignature[i])
                return false;
        }

        return true;
    }

    private static void WriteUInt32BigEndian(BinaryWriter writer, uint value)
    {
        writer.Write((byte)(value >> 24));
        writer.Write((byte)(value >> 16));
        writer.Write((byte)(value >> 8));
        writer.Write((byte)value);
    }
}
