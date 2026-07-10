using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MacBundle;

internal static class IcnsWriter
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static bool TryCreateFromIco(
        string sourceImagePath,
        string targetIcnsPath,
        Action<string>? logWarning
    )
    {
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
                    "Icon '{0}' is not a valid ICO image.",
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
                    "Icon '{0}' is not a valid ICO image.",
                    sourceImagePath
                )
            );
            return false;
        }

        // Collect PNG data for each distinct square image size found in the ICO file.
        // Sizes that don't correspond to a known ICNS type tag are skipped when writing.
        var pngBySize = new Dictionary<int, byte[]>();
        var hasPngBySize = new HashSet<int>();

        for (var i = 0; i < entryCount; i++)
        {
            var entryOffset = 6 + i * 16;
            var width = icoData[entryOffset] == 0 ? 256 : icoData[entryOffset];
            var height = icoData[entryOffset + 1] == 0 ? 256 : icoData[entryOffset + 1];

            // ICNS only supports square images
            if (width != height)
                continue;

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

            if (HasPngSignature(icoData, imageOffset))
            {
                var pngData = new byte[bytesInRes];
                Buffer.BlockCopy(icoData, imageOffset, pngData, 0, bytesInRes);
                pngBySize[width] = pngData;
                hasPngBySize.Add(width);
                continue;
            }

            if (
                !TryDecodeBitmapFromIcoFrame(
                    icoData.AsSpan(imageOffset, bytesInRes),
                    width,
                    height,
                    out var bitmap
                )
            )
            {
                continue;
            }

            // Prefer a native PNG entry over an encoded bitmap for the same size
            if (!hasPngBySize.Contains(width))
                pngBySize[width] = MacIcons.EncodeBitmapToPng(bitmap);
        }

        var icons = MacIcons.FromSizedImages(pngBySize);
        if (icons is null)
        {
            logWarning?.Invoke(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Icon '{0}' does not contain supported icon entries.",
                    sourceImagePath
                )
            );
            return false;
        }

        using var targetStream = File.Create(targetIcnsPath);
        icons.Write(targetStream);
        return true;
    }

    private static bool TryDecodeBitmapFromIcoFrame(
        ReadOnlySpan<byte> imageData,
        int fallbackWidth,
        int fallbackHeight,
        out Bitmap? bitmap
    )
    {
        bitmap = null;

        if (imageData.Length < 40)
            return false;

        var headerSize = (int)ReadUInt32LittleEndian(imageData, 0);
        if (headerSize < 40 || headerSize > imageData.Length)
            return false;

        var widthRaw = ReadInt32LittleEndian(imageData, 4);
        var heightRaw = ReadInt32LittleEndian(imageData, 8);
        var width = widthRaw == 0 ? fallbackWidth : Math.Abs(widthRaw);
        var totalHeight = heightRaw == 0 ? fallbackHeight * 2 : Math.Abs(heightRaw);
        if (width <= 0 || totalHeight < 2)
            return false;

        var height = totalHeight / 2;
        var planes = ReadUInt16LittleEndian(imageData, 12);
        var bitsPerPixel = ReadUInt16LittleEndian(imageData, 14);
        var compression = ReadUInt32LittleEndian(imageData, 16);
        if (planes != 1 || compression != 0 || (bitsPerPixel != 24 && bitsPerPixel != 32))
            return false;

        var colorDataOffset = headerSize;
        var xorStride = ((width * bitsPerPixel + 31) / 32) * 4;
        var xorDataSize = checked(xorStride * height);
        var andStride = ((width + 31) / 32) * 4;
        var andDataSize = checked(andStride * height);
        if (colorDataOffset + xorDataSize + andDataSize > imageData.Length)
            return false;

        var rgbaData = new byte[checked(width * height * 4)];

        for (var y = 0; y < height; y++)
        {
            var sourceY = height - 1 - y;
            var xorRowOffset = colorDataOffset + sourceY * xorStride;
            var andRowOffset = colorDataOffset + xorDataSize + sourceY * andStride;

            for (var x = 0; x < width; x++)
            {
                byte r;
                byte g;
                byte b;
                byte a;

                if (bitsPerPixel == 32)
                {
                    var pixelOffset = xorRowOffset + x * 4;
                    b = imageData[pixelOffset];
                    g = imageData[pixelOffset + 1];
                    r = imageData[pixelOffset + 2];
                    a = imageData[pixelOffset + 3];
                }
                else
                {
                    var pixelOffset = xorRowOffset + x * 3;
                    b = imageData[pixelOffset];
                    g = imageData[pixelOffset + 1];
                    r = imageData[pixelOffset + 2];
                    a = 255;
                }

                var maskBit = (imageData[andRowOffset + x / 8] >> (7 - (x % 8))) & 1;
                if (maskBit != 0)
                    a = 0;

                var targetOffset = (y * width + x) * 4;
                rgbaData[targetOffset] = r;
                rgbaData[targetOffset + 1] = g;
                rgbaData[targetOffset + 2] = b;
                rgbaData[targetOffset + 3] = a;
            }
        }

        bitmap = new Bitmap(width, height, rgbaData);
        return true;
    }

    private static ushort ReadUInt16LittleEndian(ReadOnlySpan<byte> data, int offset) =>
        (ushort)(data[offset] | (data[offset + 1] << 8));

    private static ushort ReadUInt16LittleEndian(byte[] data, int offset) =>
        ReadUInt16LittleEndian(data.AsSpan(), offset);

    private static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> data, int offset) =>
        (uint)(
            data[offset]
            | (data[offset + 1] << 8)
            | (data[offset + 2] << 16)
            | (data[offset + 3] << 24)
        );

    private static uint ReadUInt32LittleEndian(byte[] data, int offset) =>
        ReadUInt32LittleEndian(data.AsSpan(), offset);

    private static int ReadInt32LittleEndian(ReadOnlySpan<byte> data, int offset) =>
        unchecked((int)ReadUInt32LittleEndian(data, offset));

    private static bool HasPngSignature(byte[] data, int offset)
    {
        for (var i = 0; i < PngSignature.Length; i++)
        {
            if (data[offset + i] != PngSignature[i])
                return false;
        }

        return true;
    }
}
