using System.Collections.Generic;

namespace MacBundle.Graphics;

internal partial class Icon(IReadOnlyList<Image> bitmaps)
{
    public IReadOnlyList<Image> Images { get; } = bitmaps;
}
