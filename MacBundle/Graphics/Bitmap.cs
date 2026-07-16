using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MacBundle;

internal record Bitmap(int Width, int Height, byte[] Rgba32)
{
    public byte[] ToPngBytes()
    {
        using var image = Image.LoadPixelData<Rgba32>(Rgba32, Width, Height);
        using var stream = new MemoryStream();

        image.SaveAsPng(stream);

        return stream.ToArray();
    }

    public static Bitmap FromPngBytes(ReadOnlySpan<byte> pngBytes)
    {
        using var image = Image.Load<Rgba32>(pngBytes);

        var rgbaData = new byte[checked(image.Width * image.Height * 4)];
        image.CopyPixelDataTo(rgbaData);

        return new Bitmap(image.Width, image.Height, rgbaData);
    }
}
