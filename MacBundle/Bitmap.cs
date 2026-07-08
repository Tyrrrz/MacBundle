using System;

namespace MacBundle;

internal sealed class Bitmap
{
    public int Width { get; }

    public int Height { get; }

    public byte[] Rgba32 { get; }

    public Bitmap(int width, int height, byte[] rgba32)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (rgba32.Length != width * height * 4)
            throw new ArgumentException("Unexpected pixel buffer size.", nameof(rgba32));

        Width = width;
        Height = height;
        Rgba32 = rgba32;
    }
}
