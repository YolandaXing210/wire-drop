using System;
using System.Collections.Generic;
using System.Drawing;

namespace WireDrop.UI
{
    /// <summary>
    /// Flows the category chips across as many rows as they need. Pure geometry over
    /// pre-measured widths so the wrapping can be tested without a Rhino install.
    /// </summary>
    internal static class ChipFlow
    {
        public static Rectangle[] Arrange(int[] widths, int availableWidth, int chipHeight,
                                          int gap, int padX, int padY)
        {
            if (widths == null || widths.Length == 0) return Array.Empty<Rectangle>();

            var usable = Math.Max(chipHeight, availableWidth - padX * 2);
            var result = new List<Rectangle>(widths.Length);
            var x = padX;
            var y = padY;

            foreach (var raw in widths)
            {
                // A chip wider than the panel still gets its own row rather than vanishing.
                var w = Math.Min(Math.Max(raw, 1), usable);
                if (x > padX && x + w > padX + usable)
                {
                    x = padX;
                    y += chipHeight + gap;
                }
                result.Add(new Rectangle(x, y, w, chipHeight));
                x += w + gap;
            }
            return result.ToArray();
        }

        /// <summary>Total height the arranged chips occupy, including the bottom padding.</summary>
        public static int Height(Rectangle[] arranged, int chipHeight, int padY)
        {
            if (arranged == null || arranged.Length == 0) return chipHeight + padY * 2;
            var bottom = 0;
            foreach (var r in arranged) bottom = Math.Max(bottom, r.Bottom);
            return bottom + padY;
        }
    }
}
