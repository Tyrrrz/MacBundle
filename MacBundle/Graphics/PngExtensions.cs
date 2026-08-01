using System.IO;
using PowerKit;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MacBundle.Graphics;

internal static class PngExtensions
{
    extension(Image image)
    {
        public void SavePng(Stream stream)
        {
            using var imageSharp = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(
                image.Rgba32,
                image.Width,
                image.Height
            );
            imageSharp.SaveAsPng(stream);
        }

        public static Image LoadPng(Stream stream)
        {
            using var imageSharp = SixLabors.ImageSharp.Image.Load<Rgba32>(stream);

            using var buffer = SpanPool<byte>.Shared.Rent(
                imageSharp.Width * imageSharp.Height * imageSharp.PixelType.BitsPerPixel / 8
            );

            imageSharp.CopyPixelDataTo(buffer.Span);

            return new Image(buffer.Span.ToArray(), imageSharp.Width, imageSharp.Height);
        }
    }
}
