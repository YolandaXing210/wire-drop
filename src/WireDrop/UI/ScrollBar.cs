using System;

namespace WireDrop.UI
{
    /// <summary>
    /// Scrollbar geometry, and its inverse for dragging the thumb. Pure arithmetic so the
    /// round trip — scroll to thumb position and back — can be tested without a Rhino install.
    /// </summary>
    internal static class ScrollBar
    {
        public const int MinThumb = 24;

        public static int MaxScroll(int total, int viewport) => Math.Max(0, total - viewport);

        public static bool Needed(int total, int viewport) => total > viewport;

        public static int ThumbHeight(int total, int viewport, int trackHeight)
        {
            if (total <= 0 || viewport <= 0) return trackHeight;
            var proportional = (int)((long)trackHeight * viewport / total);
            return Math.Max(Math.Min(MinThumb, trackHeight), Math.Min(trackHeight, proportional));
        }

        public static int Travel(int total, int viewport, int trackHeight) =>
            Math.Max(0, trackHeight - ThumbHeight(total, viewport, trackHeight));

        /// <summary>Thumb top, relative to the top of the track.</summary>
        public static int ThumbTop(int total, int viewport, int trackHeight, int scroll)
        {
            var max = MaxScroll(total, viewport);
            if (max <= 0) return 0;
            var travel = Travel(total, viewport, trackHeight);
            var clamped = Math.Max(0, Math.Min(scroll, max));
            return (int)Math.Round((double)travel * clamped / max);
        }

        /// <summary>The scroll offset that puts the thumb at <paramref name="thumbTop"/>.</summary>
        public static int ScrollFromThumbTop(int total, int viewport, int trackHeight, int thumbTop)
        {
            var max = MaxScroll(total, viewport);
            if (max <= 0) return 0;
            var travel = Travel(total, viewport, trackHeight);
            if (travel <= 0) return max;
            var clamped = Math.Max(0, Math.Min(thumbTop, travel));
            var scroll = (int)Math.Round((double)max * clamped / travel);
            return Math.Max(0, Math.Min(scroll, max));
        }
    }
}
