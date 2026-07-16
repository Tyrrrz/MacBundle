using System.Collections.Generic;

namespace MacBundle;

internal partial class Icon(IReadOnlyList<Bitmap> bitmaps)
{
    public IReadOnlyList<Bitmap> Bitmaps { get; } = bitmaps;
}
