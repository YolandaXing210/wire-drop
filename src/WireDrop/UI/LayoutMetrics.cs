using System;

namespace WireDrop.UI
{
    /// <summary>
    /// The panel's proportions as pure arithmetic over the app font's line heights, so
    /// the layout can be reasoned about and tested without a Rhino install. Every value
    /// grows with the font and none falls below a legible floor.
    /// </summary>
    internal readonly struct LayoutMetrics
    {
        public const int VisibleRows = 14;
        /// <summary>
        /// Two lines for the component's own description, one for the port's — measured in
        /// body lines, since the strip is set in the same font as the rows above it.
        /// </summary>
        public const int HelpLines = 3;

        public readonly int Pad;
        public readonly int RowH;
        public readonly int HeadH;
        public readonly int ChipH;
        public readonly int ChipGap;
        public readonly int FootH;
        public readonly int ListH;
        public readonly int IconSize;
        public readonly int HelpH;

        LayoutMetrics(int bodyHeight, int smallHeight)
        {
            var line = Math.Max(bodyHeight, 11);
            var small = Math.Max(smallHeight, 9);

            Pad = Math.Max(6, line / 2);
            RowH = Math.Max(20, line + 8);
            HeadH = Math.Max(15, small + 5);
            ChipH = Math.Max(15, small + 5);
            ChipGap = Math.Max(3, small / 3);
            FootH = Math.Max(19, small + 6);
            ListH = RowH * VisibleRows;
            IconSize = Math.Min(24, Math.Max(16, RowH - 8));
            HelpH = line * HelpLines + Pad;
        }

        public static LayoutMetrics From(int bodyHeight, int smallHeight) =>
            new LayoutMetrics(bodyHeight, smallHeight);
    }
}
