using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MacBundle.Utils;
using PowerKit.Extensions;

namespace MacBundle.Graphics;

internal partial class Icon
{
    private static bool TryDecodeBitmapFromIcoFrame(
        ReadOnlySpan<byte> imageData,
        int fallbackWidth,
        int fallbackHeight,
        out Image? image
    )
    {
        image = default;

        if (imageData.Length < 40)
            return false;

        var headerSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(imageData));
        if (headerSize < 40 || headerSize > imageData.Length)
            return false;

        var widthRaw = BinaryPrimitives.ReadInt32LittleEndian(imageData[4..]);
        var heightRaw = BinaryPrimitives.ReadInt32LittleEndian(imageData[8..]);
        var width = widthRaw == 0 ? fallbackWidth : Math.Abs(widthRaw);
        var totalHeight = heightRaw == 0 ? fallbackHeight * 2 : Math.Abs(heightRaw);
        if (width <= 0 || totalHeight < 2)
            return false;

        var height = totalHeight / 2;
        var planes = BinaryPrimitives.ReadUInt16LittleEndian(imageData[12..]);
        var bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(imageData[14..]);
        var compression = BinaryPrimitives.ReadUInt32LittleEndian(imageData[16..]);
        if (planes != 1 || compression != 0 || (bitsPerPixel != 24 && bitsPerPixel != 32))
            return false;

        var colorDataOffset = headerSize;
        var xorStride = checked((width * bitsPerPixel + 31) / 32 * 4);
        var xorDataSize = checked(xorStride * height);
        var andStride = checked((width + 31) / 32 * 4);
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

                var maskBit = (imageData[andRowOffset + x / 8] >> (7 - x % 8)) & 1;
                if (maskBit != 0)
                    a = 0;

                var targetOffset = (y * width + x) * 4;
                rgbaData[targetOffset] = r;
                rgbaData[targetOffset + 1] = g;
                rgbaData[targetOffset + 2] = b;
                rgbaData[targetOffset + 3] = a;
            }
        }

        image = new Image(rgbaData, width, height);
        return true;
    }

    private static Icon LoadIcoFromSeekable(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        // -- Header

        // Reserved (always zero), type (always one)
        if (reader.ReadUInt16() != 0 || reader.ReadUInt16() != 1)
            throw new InvalidDataException("The stream does not contain a valid ICO image.");

        // Image count
        var imageCount = reader.ReadUInt16();
        var images = new List<Image>(imageCount);

        // -- Image directory
        for (var i = 0; i < imageCount; i++)
        {
            // Width, height
            var widthByte = reader.ReadByte();
            var heightByte = reader.ReadByte();
            var width = widthByte == 0 ? 256 : widthByte;
            var height = heightByte == 0 ? 256 : heightByte;

            // Color count, reserved, color planes, bits per pixel
            _ = reader.ReadByte();
            _ = reader.ReadByte();
            _ = reader.ReadUInt16();
            _ = reader.ReadUInt16();

            // Image data length, offset
            var imageDataLength = reader.ReadUInt32();
            var imageDataOffset = reader.ReadUInt32();
            if (
                imageDataOffset > stream.Length
                || imageDataLength > stream.Length - imageDataOffset
            )
            {
                throw new InvalidDataException(
                    "The ICO image contains an invalid image-data range."
                );
            }

            if (imageDataLength < Png.Signature.Length)
                continue;

            var imageDataPortal = stream.CreatePortal(imageDataOffset);

            // Image data (PNG)
            Span<byte> signature = stackalloc byte[Png.Signature.Length];
            using (imageDataPortal.Jump())
                stream.ReadExactly(signature);

            if (Png.StartsWithSignature(signature))
            {
                using (imageDataPortal.Jump())
                    images.Add(Image.LoadPng(stream));

                continue;
            }

            // Image data (BMP)
            var imageData = new byte[checked((int)imageDataLength)];
            using (imageDataPortal.Jump())
                stream.ReadExactly(imageData);

            if (TryDecodeBitmapFromIcoFrame(imageData, width, height, out var image))
                images.Add(image);
        }

        return new Icon(images);
    }

    public static Icon LoadIco(Stream stream)
    {
        if (stream.CanSeek)
            return LoadIcoFromSeekable(stream);

        using var seekableStream = new MemoryStream();
        stream.CopyTo(seekableStream);
        seekableStream.Position = 0;

        return LoadIcoFromSeekable(seekableStream);
    }
}
