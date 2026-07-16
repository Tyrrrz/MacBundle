using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using MacBundle.Utils;
using MacBundle.Utils.Extensions;

namespace MacBundle;

internal partial class Icon
{
    private static bool TryDecodeBitmapFromIcoFrame(
        ReadOnlySpan<byte> imageData,
        int fallbackWidth,
        int fallbackHeight,
        out Bitmap? bitmap
    )
    {
        bitmap = default;

        if (imageData.Length < 40)
            return false;

        var headerSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(imageData[..sizeof(uint)]);
        if (headerSize < 40 || headerSize > imageData.Length)
            return false;

        var widthRaw = BinaryPrimitives.ReadInt32LittleEndian(imageData.Slice(4, sizeof(int)));
        var heightRaw = BinaryPrimitives.ReadInt32LittleEndian(imageData.Slice(8, sizeof(int)));
        var width = widthRaw == 0 ? fallbackWidth : Math.Abs(widthRaw);
        var totalHeight = heightRaw == 0 ? fallbackHeight * 2 : Math.Abs(heightRaw);
        if (width <= 0 || totalHeight < 2)
            return false;

        var height = totalHeight / 2;
        var planes = BinaryPrimitives.ReadUInt16LittleEndian(imageData.Slice(12, sizeof(ushort)));
        var bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(
            imageData.Slice(14, sizeof(ushort))
        );
        var compression = BinaryPrimitives.ReadUInt32LittleEndian(
            imageData.Slice(16, sizeof(uint))
        );
        if (planes != 1 || compression != 0 || (bitsPerPixel != 24 && bitsPerPixel != 32))
            return false;

        var colorDataOffset = headerSize;
        var xorStride = (width * bitsPerPixel + 31) / 32 * 4;
        var xorDataSize = checked(xorStride * height);
        var andStride = (width + 31) / 32 * 4;
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

    public static Icon LoadIco(Stream stream)
    {
        var icoData = stream.ReadAllBytes();

        if (
            icoData.Length < 6
            || BinaryPrimitives.ReadUInt16LittleEndian(icoData.AsSpan(0, sizeof(ushort))) != 0
            || BinaryPrimitives.ReadUInt16LittleEndian(icoData.AsSpan(2, sizeof(ushort))) != 1
        )
        {
            throw new InvalidDataException("The stream does not contain a valid ICO image.");
        }

        var entryCount = BinaryPrimitives.ReadUInt16LittleEndian(icoData.AsSpan(4, sizeof(ushort)));
        if (entryCount == 0 || icoData.Length < 6 + entryCount * 16)
        {
            throw new InvalidDataException("The stream does not contain a valid ICO image.");
        }

        var bitmapsBySize = new Dictionary<int, Bitmap>();
        var hasPngBySize = new HashSet<int>();

        for (var i = 0; i < entryCount; i++)
        {
            var entryOffset = 6 + i * 16;
            var width = icoData[entryOffset] == 0 ? 256 : icoData[entryOffset];
            var height = icoData[entryOffset + 1] == 0 ? 256 : icoData[entryOffset + 1];

            if (width != height)
                continue;

            var bytesInRes = (int)
                BinaryPrimitives.ReadUInt32LittleEndian(
                    icoData.AsSpan(entryOffset + 8, sizeof(uint))
                );

            var imageOffset = (int)
                BinaryPrimitives.ReadUInt32LittleEndian(
                    icoData.AsSpan(entryOffset + 12, sizeof(uint))
                );

            var imageData = icoData.AsSpan(imageOffset, bytesInRes);
            if (Png.StartsWithSignature(imageData))
            {
                bitmapsBySize[width] = Bitmap.FromPngBytes(imageData);
                hasPngBySize.Add(width);
                continue;
            }

            if (!TryDecodeBitmapFromIcoFrame(imageData, width, height, out var bitmap))
                continue;

            // Prefer native PNG entries over raw BMP payloads for the same size
            if (!hasPngBySize.Contains(width))
                bitmapsBySize[width] = bitmap;
        }

        if (bitmapsBySize.Count == 0)
        {
            throw new InvalidDataException(
                "The ICO image does not contain supported icon entries."
            );
        }

        return new Icon([.. bitmapsBySize.Values]);
    }
}
