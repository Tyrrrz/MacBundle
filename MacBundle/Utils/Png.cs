using System;

namespace MacBundle.Utils;

internal static class Png
{
    public static ReadOnlySpan<byte> Signature => "\x89PNG\r\n\x1A\n"u8;

    public static bool StartsWithSignature(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8 && bytes[..8].SequenceEqual(Signature);
}
