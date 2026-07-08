using System;

namespace MacBundle;

internal class Bitmap(int width, int height, byte[] rgba32)
{
    public int Width { get; } =
        width >= 1 ? width : throw new ArgumentOutOfRangeException(nameof(width));

    public int Height { get; } =
        height >= 1 ? height : throw new ArgumentOutOfRangeException(nameof(height));

    public byte[] Rgba32 { get; } =
        rgba32.Length == width * height * 4
            ? rgba32
            : throw new ArgumentException("Unexpected pixel buffer size.", nameof(rgba32));
}
