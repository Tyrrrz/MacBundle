using System;
using System.IO;
using System.Text;
using PowerKit;
using PowerKit.Extensions;
using SixLabors.ImageSharp;

namespace MacBundle.Graphics;

internal static class IcoExtensions
{
    private static Image? TryDecodePngFromIcoFrame(Stream stream)
    {
        try
        {
            return Image.LoadPng(stream);
        }
        catch (UnknownImageFormatException)
        {
            return null;
        }
    }

    private static Image? TryDecodeBmpFromIcoFrame(
        Stream stream,
        int fallbackWidth,
        int fallbackHeight
    )
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        var headerSize = checked((int)reader.ReadUInt32());
        if (headerSize < 40 || headerSize > stream.Length)
            return null;

        var widthRaw = reader.ReadInt32();
        var width = widthRaw != 0 ? Math.Abs(widthRaw) : fallbackWidth;
        var totalHeightRaw = reader.ReadInt32();
        var totalHeight = totalHeightRaw != 0 ? Math.Abs(totalHeightRaw) : fallbackHeight * 2;
        var height = totalHeight / 2;
        if (width <= 0 || height <= 0)
            return null;

        var colorPlanes = reader.ReadUInt16();
        var bitsPerPixel = reader.ReadUInt16();
        var compression = reader.ReadUInt32();
        if (colorPlanes != 1 || compression != 0 || (bitsPerPixel != 24 && bitsPerPixel != 32))
            return null;

        var colorDataOffset = headerSize;
        var xorStride = checked((width * bitsPerPixel + 31) / 32 * 4);
        var xorDataSize = checked(xorStride * height);
        var andStride = checked((width + 31) / 32 * 4);
        var andDataSize = checked(andStride * height);
        if (colorDataOffset + xorDataSize + andDataSize > stream.Length)
            return null;

        var rgbaData = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++)
        {
            var sourceY = height - 1 - y;
            var xorRowOffset = colorDataOffset + sourceY * xorStride;
            var andRowOffset = colorDataOffset + xorDataSize + sourceY * andStride;

            for (var x = 0; x < width; x++)
            {
                var pixelOffset = xorRowOffset + x * (bitsPerPixel / 8);
                stream.Position = pixelOffset;

                var b = reader.ReadByte();
                var g = reader.ReadByte();
                var r = reader.ReadByte();
                var a = bitsPerPixel == 32 ? reader.ReadByte() : (byte)255;

                stream.Position = andRowOffset + x / 8;
                var maskBit = (reader.ReadByte() >> (7 - x % 8)) & 1;
                if (maskBit != 0)
                    a = 0;

                var targetOffset = (y * width + x) * 4;
                rgbaData[targetOffset] = r;
                rgbaData[targetOffset + 1] = g;
                rgbaData[targetOffset + 2] = b;
                rgbaData[targetOffset + 3] = a;
            }
        }

        return new Image(rgbaData, width, height);
    }

    private static Icon LoadIcoFromSeekable(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        // Reserved (always zero), type (always one)
        if (reader.ReadUInt16() != 0 || reader.ReadUInt16() != 1)
        {
            throw new InvalidDataException(
                "Invalid ICO format: missing or unexpected magic number."
            );
        }

        var imageCount = reader.ReadUInt16();
        var images = new Image[imageCount];
        for (var i = 0; i < imageCount; i++)
        {
            var width = reader.ReadByte();
            var height = reader.ReadByte();
            var colorCount = reader.ReadByte();
            _ = reader.ReadByte(); // reserved
            var colorPlanes = reader.ReadUInt16();
            var bitsPerPixel = reader.ReadUInt16();
            var dataLength = reader.ReadUInt32();
            var dataOffset = reader.ReadUInt32();

            var data = new byte[checked((int)dataLength)];
            using (stream.CreatePortal(dataOffset).Jump())
                stream.ReadExactly(data);

            using var dataStream = new MemoryStream(data, false);

            var image =
                TryDecodePngFromIcoFrame(dataStream)
                ?? TryDecodeBmpFromIcoFrame(dataStream, width, height);

            if (image is not null)
                images[i] = image;
        }

        return new Icon(images);
    }

    extension(Stream stream)
    {
        public Icon LoadIco()
        {
            if (!stream.CanSeek)
            {
                using var seekableStream = new MemoryReadStream(stream);
                return LoadIcoFromSeekable(seekableStream);
            }

            return LoadIcoFromSeekable(stream);
        }
    }
}
