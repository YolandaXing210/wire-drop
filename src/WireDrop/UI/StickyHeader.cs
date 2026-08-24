using System;

namespace WireDrop.UI
{
    /// <summary>
    /// Where to draw the pinned band heading. Only one is ever pinned — the band the top
    /// of the list is currently inside — and it is pushed up out of view by the next
    /// heading as that heading arrives, so the two never overlap.
    /// </summary>
    internal readonly struct StickyHeader
    {
        /// <summary>Index into the row array, or -1 when nothing should be pinned.</summary>
        public readonly int Index;
        /// <summary>Y offset from the top of the list; goes negative as it is pushed out.</summary>
        public readonly int Offset;

        public bool Visible => Index >= 0;

        StickyHeader(int index, int offset) { Index = index; Offset = offset; }

        public static readonly StickyHeader None = new StickyHeader(-1, 0);

        /// <param name="isHeader">Whether row i is a band heading.</param>
        /// <param name="offsets">Cumulative row tops; length is rowCount + 1.</param>
        /// <param name="scroll">Current scroll offset in pixels.</param>
        /// <param name="headerHeight">Height of a band heading.</param>
        public static StickyHeader Resolve(Func<int, bool> isHeader, int[] offsets, int rowCount,
                                           int scroll, int headerHeight)
        {
            if (offsets == null || rowCount <= 0) return None;

            var current = -1;
            var next = -1;
            for (int i = 0; i < rowCount; i++)
            {
                if (!isHeader(i)) continue;
                if (offsets[i] <= scroll) current = i;
                else { next = i; break; }
            }

            if (current < 0) return None;

            // Once the following heading comes within one header of the top, it starts
            // shouldering the pinned one out rather than sliding underneath it.
            var offset = 0;
            if (next >= 0)
            {
                var gap = offsets[next] - scroll;
                if (gap < headerHeight) offset = gap - headerHeight;
            }
            return new StickyHeader(current, offset);
        }
    }
}
