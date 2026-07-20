using System.IO;
using PowerKit;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MacBundle.Graphics;

internal record Image(byte[] Rgba32, int Width, int Height)
{
    public void SavePng(Stream stream)
    {
        using var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(Rgba32, Width, Height);
        image.SaveAsPng(stream);
    }

    public static Image LoadPng(Stream stream)
    {
        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(stream);

        using var buffer = SpanPool<byte>.Shared.Rent(
            image.Width * image.Height * image.PixelType.BitsPerPixel / 8
        );

        image.CopyPixelDataTo(buffer.Span);

        return new Image(buffer.Span.ToArray(), image.Width, image.Height);
    }
}
