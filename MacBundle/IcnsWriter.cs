using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MacBundle;

internal static class IcnsWriter
{
    private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

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

        var pngData = File.ReadAllBytes(sourceImagePath);
        if (
            pngData.Length < PngSignature.Length
            || !pngData.Take(PngSignature.Length).SequenceEqual(PngSignature)
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

        var payloadLength = checked((uint)(8 + pngData.Length));
        var totalLength = checked((uint)(8 + payloadLength));

        using var stream = File.Create(targetIcnsPath);
        using var writer = new BinaryWriter(stream);

        writer.Write(new byte[] { (byte)'i', (byte)'c', (byte)'n', (byte)'s' });
        WriteUInt32BigEndian(writer, totalLength);

        writer.Write(new byte[] { (byte)'i', (byte)'c', (byte)'1', (byte)'0' });
        WriteUInt32BigEndian(writer, payloadLength);
        writer.Write(pngData);

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
