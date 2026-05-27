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
        if (
            !string.Equals(
                Path.GetExtension(sourceImagePath),
                ".png",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            logWarning?.Invoke(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ApplicationIcon '{0}' is not a .png or .icns file.",
                    sourceImagePath
                )
            );
            return false;
        }

        var sourceImageInfo = new FileInfo(sourceImagePath);
        if (sourceImageInfo.Length < PngSignature.Length)
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

        using var sourceStream = File.OpenRead(sourceImagePath);

        var signatureBuffer = new byte[PngSignature.Length];
        if (
            sourceStream.Read(signatureBuffer, 0, signatureBuffer.Length) != signatureBuffer.Length
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

        var pngLength = checked((uint)sourceImageInfo.Length);
        var payloadLength = checked(ChunkHeaderSize + pngLength);
        var totalLength = checked(IcnsHeaderSize + payloadLength);

        using var stream = File.Create(targetIcnsPath);
        using var writer = new BinaryWriter(stream);

        writer.Write(IcnsMagic);
        WriteUInt32BigEndian(writer, totalLength);

        writer.Write(Ic10Type);
        WriteUInt32BigEndian(writer, payloadLength);

        sourceStream.Position = 0;
        sourceStream.CopyTo(stream);

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
